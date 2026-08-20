#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Elden.Editor.ArtRecovery
{
    /// <summary>
    /// Rebuilds native Unity materials from the deterministic recovery plan.
    /// The original JSON remains the authoritative source for unsupported
    /// custom-shader properties.
    /// </summary>
    internal static class RecoveredMaterialRebuilder
    {
        private const string k_MenuPath = "Tools/Art Recovery/Rebuild Recovered Materials";
        private const string k_PlanRelativePath = "Docs/ArtRecovery/Nephilite/MaterialRebuild/material_rebuild_plan.json";
        private const string k_ReportRelativePath = "Docs/ArtRecovery/Nephilite/MaterialRebuild/material_rebuild_result.json";
        private const string k_CsvRelativePath = "Docs/ArtRecovery/Nephilite/MaterialRebuild/material_rebuild_report.csv";
        private const string k_RecoveredLabel = "RecoveredMaterial";
        private const string k_FallbackLabel = "RecoveredMaterialFallback";

        [MenuItem(k_MenuPath, priority = 2100)]
        public static void RebuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Material reconstruction cannot run in Play Mode.");
            }

            if (EditorApplication.isCompiling)
            {
                throw new InvalidOperationException("Wait for script compilation before rebuilding materials.");
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
            string planPath = Path.Combine(projectRoot, k_PlanRelativePath);
            if (!File.Exists(planPath))
            {
                throw new FileNotFoundException("Material rebuild plan is missing.", planPath);
            }

            JObject plan = JObject.Parse(File.ReadAllText(planPath, Encoding.UTF8));
            JArray materialPlans = plan["materials"] as JArray
                ?? throw new InvalidDataException("The material rebuild plan has no materials array.");
            JArray results = new JArray();
            int created = 0;
            int updated = 0;
            int failed = 0;
            int textureBindingsAssigned = 0;
            int unsupportedPropertyCount = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (JObject materialPlan in materialPlans.OfType<JObject>())
                {
                    JObject result = RebuildOne(materialPlan);
                    results.Add(result);
                    string status = result.Value<string>("status") ?? "failed";
                    if (status == "created")
                    {
                        created++;
                    }
                    else if (status == "updated")
                    {
                        updated++;
                    }
                    else
                    {
                        failed++;
                    }

                    textureBindingsAssigned += result.Value<int?>("textureBindingsAssigned") ?? 0;
                    unsupportedPropertyCount += result.Value<int?>("unsupportedPropertyCount") ?? 0;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            JObject summary = new JObject
            {
                ["materialCount"] = materialPlans.Count,
                ["created"] = created,
                ["updated"] = updated,
                ["failed"] = failed,
                ["textureBindingsAssigned"] = textureBindingsAssigned,
                ["unsupportedPropertyCount"] = unsupportedPropertyCount,
                ["generatedAtUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["unityVersion"] = Application.unityVersion,
                ["renderPipeline"] = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.GetType().FullName
                    : "Built-in"
            };
            JObject report = new JObject
            {
                ["schemaVersion"] = 1,
                ["summary"] = summary,
                ["materials"] = results
            };
            WriteReports(projectRoot, report, results);

            string message = $"Recovered material rebuild finished: created={created}, updated={updated}, " +
                             $"failed={failed}, textures={textureBindingsAssigned}, " +
                             $"unsupportedProperties={unsupportedPropertyCount}.";
            if (failed > 0)
            {
                Debug.LogError(message);
            }
            else
            {
                Debug.Log(message);
            }
        }

        private static JObject RebuildOne(JObject plan)
        {
            string materialName = plan.Value<string>("materialName") ?? "RecoveredMaterial";
            string targetPath = plan.Value<string>("targetAssetPath")
                ?? throw new InvalidDataException($"Material {materialName} has no target path.");
            JObject result = new JObject
            {
                ["materialName"] = materialName,
                ["metadataAssetPath"] = plan.Value<string>("metadataAssetPath") ?? string.Empty,
                ["targetAssetPath"] = targetPath,
                ["fallbackReason"] = plan.Value<string>("fallbackReason") ?? string.Empty,
                ["sourceShaderPathId"] = plan.Value<long?>("sourceShaderPathId") ?? 0,
                ["unresolvedTextureCount"] = (plan["unresolvedTextures"] as JArray)?.Count ?? 0
            };

            try
            {
                Shader shader = ResolveShader(plan, out string selectedShaderName, out bool usedFallback);
                result["shader"] = selectedShaderName;
                result["usedFallbackShader"] = usedFallback;

                Material material = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
                bool isNew = material == null;
                if (!isNew && !AssetDatabase.GetLabels(material).Contains(k_RecoveredLabel))
                {
                    throw new InvalidOperationException($"Refusing to overwrite a non-recovered material: {targetPath}");
                }

                if (isNew)
                {
                    EnsureAssetFolder(Path.GetDirectoryName(targetPath)?.Replace('\\', '/') ?? "Assets");
                    material = new Material(shader) { name = materialName };
                    AssetDatabase.CreateAsset(material, targetPath);
                }
                else
                {
                    material.shader = shader;
                    material.name = materialName;
                }

                HashSet<string> unsupported = new HashSet<string>(StringComparer.Ordinal);
                int supportedValues = 0;
                supportedValues += ApplyFloats(material, plan["floats"] as JObject, unsupported);
                supportedValues += ApplyInts(material, plan["ints"] as JObject, unsupported);
                supportedValues += ApplyColors(material, plan["colors"] as JObject, unsupported);
                int textureBindingsAssigned = ApplyTextures(
                    material,
                    plan["textures"] as JArray,
                    plan.Value<string>("primaryTextureAssetPath") ?? string.Empty,
                    unsupported,
                    out bool usedRepresentativeTexture);
                ConfigureKeywordsAndQueue(material);
                EditorUtility.SetDirty(material);

                List<string> labels = AssetDatabase.GetLabels(material).ToList();
                if (!labels.Contains(k_RecoveredLabel))
                {
                    labels.Add(k_RecoveredLabel);
                }

                if (usedFallback && !labels.Contains(k_FallbackLabel))
                {
                    labels.Add(k_FallbackLabel);
                }

                if (!usedFallback)
                {
                    labels.Remove(k_FallbackLabel);
                }

                AssetDatabase.SetLabels(material, labels.ToArray());

                result["status"] = isNew ? "created" : "updated";
                result["supportedValueCount"] = supportedValues;
                result["textureBindingsRequested"] = (plan["textures"] as JArray)?.Count ?? 0;
                result["textureBindingsAssigned"] = textureBindingsAssigned;
                result["usedRepresentativeTexture"] = usedRepresentativeTexture;
                result["unsupportedPropertyCount"] = unsupported.Count;
                result["unsupportedProperties"] = new JArray(unsupported.OrderBy(value => value, StringComparer.Ordinal));
            }
            catch (Exception exception)
            {
                result["status"] = "failed";
                result["error"] = exception.ToString();
                result["textureBindingsAssigned"] = 0;
                result["unsupportedPropertyCount"] = 0;
            }
            return result;
        }

        private static Shader ResolveShader(JObject plan, out string selectedName, out bool usedFallback)
        {
            string fallback = plan.Value<string>("fallbackShader") ?? "Universal Render Pipeline/Lit";
            IEnumerable<string> candidates = (plan["shaderCandidates"] as JArray)?.Values<string>()
                ?? Enumerable.Empty<string>();
            foreach (string candidate in candidates.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                Shader found = Shader.Find(candidate);
                if (found == null)
                {
                    continue;
                }

                selectedName = candidate;
                usedFallback = string.Equals(candidate, fallback, StringComparison.Ordinal);
                return found;
            }
            Shader fallbackShader = Shader.Find(fallback);
            if (fallbackShader == null)
            {
                throw new InvalidOperationException($"No planned shader could be resolved; fallback={fallback}.");
            }

            selectedName = fallback;
            usedFallback = true;
            return fallbackShader;
        }

        private static int ApplyFloats(Material material, JObject values, ISet<string> unsupported)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            foreach (JProperty entry in values.Properties())
            {
                string target = ResolveProperty(material, entry.Name, PropertyKind.Float);
                if (target == null)
                {
                    unsupported.Add("Float:" + entry.Name);
                    continue;
                }

                material.SetFloat(target, entry.Value.Value<float>());
                count++;
            }
            return count;
        }

        private static int ApplyInts(Material material, JObject values, ISet<string> unsupported)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            foreach (JProperty entry in values.Properties())
            {
                string target = ResolveProperty(material, entry.Name, PropertyKind.Float);
                if (target == null)
                {
                    unsupported.Add("Int:" + entry.Name);
                    continue;
                }

                material.SetInt(target, entry.Value.Value<int>());
                count++;
            }
            return count;
        }

        private static int ApplyColors(Material material, JObject values, ISet<string> unsupported)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            foreach (JProperty entry in values.Properties())
            {
                string target = ResolveProperty(material, entry.Name, PropertyKind.Color);
                if (target == null)
                {
                    unsupported.Add("Color:" + entry.Name);
                    continue;
                }

                JObject color = entry.Value as JObject ?? new JObject();
                material.SetColor(target, new Color(
                    color.Value<float?>("r") ?? 0f,
                    color.Value<float?>("g") ?? 0f,
                    color.Value<float?>("b") ?? 0f,
                    color.Value<float?>("a") ?? 1f));
                count++;
            }
            return count;
        }

        private static int ApplyTextures(
            Material material,
            JArray textures,
            string primaryTexturePath,
            ISet<string> unsupported,
            out bool usedRepresentativeTexture)
        {
            usedRepresentativeTexture = false;
            if (textures == null)
            {
                return 0;
            }

            int assigned = 0;
            foreach (JObject texturePlan in textures.OfType<JObject>())
            {
                string sourceProperty = texturePlan.Value<string>("sourceProperty") ?? string.Empty;
                string targetProperty = ResolveProperty(material, sourceProperty, PropertyKind.Texture);
                if (targetProperty == null)
                {
                    unsupported.Add("Texture:" + sourceProperty);
                    continue;
                }
                string assetPath = texturePlan.Value<string>("assetPath") ?? string.Empty;
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);
                if (texture == null)
                {
                    unsupported.Add("MissingTexture:" + sourceProperty + "=" + assetPath);
                    continue;
                }
                material.SetTexture(targetProperty, texture);
                JObject scale = texturePlan["scale"] as JObject;
                JObject offset = texturePlan["offset"] as JObject;
                material.SetTextureScale(targetProperty, new Vector2(
                    scale?.Value<float?>("x") ?? 1f,
                    scale?.Value<float?>("y") ?? 1f));
                material.SetTextureOffset(targetProperty, new Vector2(
                    offset?.Value<float?>("x") ?? 0f,
                    offset?.Value<float?>("y") ?? 0f));
                assigned++;
            }

            string baseProperty = ResolveProperty(material, "_BaseMap", PropertyKind.Texture);
            if (!string.IsNullOrEmpty(primaryTexturePath) && baseProperty != null && material.GetTexture(baseProperty) == null)
            {
                Texture representative = AssetDatabase.LoadAssetAtPath<Texture>(primaryTexturePath);
                if (representative != null)
                {
                    material.SetTexture(baseProperty, representative);
                    usedRepresentativeTexture = true;
                }
            }
            return assigned;
        }

        private static string ResolveProperty(Material material, string source, PropertyKind kind)
        {
            IEnumerable<string> candidates = PropertyAliases(source, kind);
            return candidates.FirstOrDefault(material.HasProperty);
        }

        private static IEnumerable<string> PropertyAliases(string source, PropertyKind kind)
        {
            if (kind == PropertyKind.Texture)
            {
                if (source is "_MainTex" or "_BaseMap" or "_Albedo" or "_AlbedoMap" or "_DiffuseMap" or "_ColorMap" or "_Tex")
                {
                    return new[] { "_BaseMap", "_MainTex" };
                }

                if (source is "_NormalMap")
                {
                    return new[] { "_BumpMap", source };
                }

                if (source is "_MetallicMap")
                {
                    return new[] { "_MetallicGlossMap", source };
                }

                if (source is "_AOMap" or "_AmbientOcclusionMap")
                {
                    return new[] { "_OcclusionMap", source };
                }

                if (source is "_EmissiveMap")
                {
                    return new[] { "_EmissionMap", source };
                }
            }
            if (kind == PropertyKind.Color)
            {
                if (source is "_Color" or "_BaseColor" or "_Tint" or "_TintColor")
                {
                    return new[] { "_BaseColor", "_Color", "_Tint" };
                }
            }
            if (kind == PropertyKind.Float)
            {
                if (source is "_Glossiness" or "_GlossMapScale")
                {
                    return new[] { "_Smoothness", source };
                }

                if (source is "_AlphaCutoff" or "_Cutout")
                {
                    return new[] { "_Cutoff", source };
                }
            }
            return new[] { source };
        }

        private static void ConfigureKeywordsAndQueue(Material material)
        {
            SetKeyword(material, "_NORMALMAP", HasTexture(material, "_BumpMap"));
            SetKeyword(material, "_METALLICSPECGLOSSMAP", HasTexture(material, "_MetallicGlossMap") || HasTexture(material, "_SpecGlossMap"));
            SetKeyword(material, "_OCCLUSIONMAP", HasTexture(material, "_OcclusionMap"));
            bool hasEmission = HasTexture(material, "_EmissionMap") ||
                               (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.0001f);
            SetKeyword(material, "_EMISSION", hasEmission);
            material.globalIlluminationFlags = hasEmission
                ? MaterialGlobalIlluminationFlags.BakedEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            bool alphaClip = material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f;
            bool transparent = material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f;
            SetKeyword(material, "_ALPHATEST_ON", alphaClip);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
            int queueOffset = material.HasProperty("_QueueOffset")
                ? Mathf.RoundToInt(material.GetFloat("_QueueOffset"))
                : 0;
            if (transparent)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent + queueOffset;
            }
            else if (alphaClip)
            {
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest + queueOffset;
            }
            else
            {
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = -1;
            }
        }

        private static bool HasTexture(Material material, string property) =>
            material.HasProperty(property) && material.GetTexture(property) != null;

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/') ?? "Assets";
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }

        private static void WriteReports(string projectRoot, JObject report, JArray results)
        {
            string reportPath = Path.Combine(projectRoot, k_ReportRelativePath);
            string csvPath = Path.Combine(projectRoot, k_CsvRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? projectRoot);
            File.WriteAllText(reportPath, report.ToString(Formatting.Indented), new UTF8Encoding(false));

            StringBuilder csv = new StringBuilder();
            csv.AppendLine("material_name,status,target_asset_path,shader,used_fallback_shader,texture_bindings_requested,texture_bindings_assigned,unresolved_texture_count,unsupported_property_count,error");
            foreach (JObject result in results.OfType<JObject>())
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Csv(result.Value<string>("materialName")),
                    Csv(result.Value<string>("status")),
                    Csv(result.Value<string>("targetAssetPath")),
                    Csv(result.Value<string>("shader")),
                    Csv(result.Value<bool?>("usedFallbackShader")?.ToString()),
                    Csv(result.Value<int?>("textureBindingsRequested")?.ToString(CultureInfo.InvariantCulture)),
                    Csv(result.Value<int?>("textureBindingsAssigned")?.ToString(CultureInfo.InvariantCulture)),
                    Csv(result.Value<int?>("unresolvedTextureCount")?.ToString(CultureInfo.InvariantCulture)),
                    Csv(result.Value<int?>("unsupportedPropertyCount")?.ToString(CultureInfo.InvariantCulture)),
                    Csv(result.Value<string>("error"))
                }));
            }
            File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(true));
        }

        private static string Csv(string value)
        {
            value ??= string.Empty;
            return "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }

        private enum PropertyKind
        {
            Float,
            Color,
            Texture
        }
    }
}
#endif
