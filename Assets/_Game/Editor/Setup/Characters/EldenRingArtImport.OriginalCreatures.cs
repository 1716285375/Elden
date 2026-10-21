using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace ZZ.Editor
{
    public static partial class EldenRingArtImport
    {
        [MenuItem("Tools/ZZ/Elden Ring/Build Original Beastman Rig")]
        public static void BuildOriginalBeastmanRig()
        {
            BuildOriginalCreatureRig("c3970", 39701910);
        }

        [MenuItem("Tools/ZZ/Elden Ring/Build Original Wolf Rig")]
        public static void BuildOriginalWolfRig()
        {
            BuildOriginalCreatureRig("c4070", 40700010);
        }

        [MenuItem("Tools/ZZ/Elden Ring/Build Original Elite Wolf Rig")]
        public static void BuildOriginalEliteWolfRig()
        {
            BuildOriginalCreatureRig("c4071", 40710010);
        }

        private static void BuildOriginalCreatureRig(string actor, int npcId)
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play Mode before building a source creature.");
            }
            const string sourceFolder = "F:/MyProject/RE/EldenRing/Converted/SingleMapCharacters";
            string output = "Assets/_Game/Art/Characters/EldenRing/OriginalCreatures/" + actor;
            foreach (string folder in new[] { "Meshes", "Materials", "Textures", "Animations" })
            {
                Directory.CreateDirectory(output + "/" + folder);
            }
            foreach (string texture in Directory.GetFiles(sourceFolder + "/Textures", "*.png"))
            {
                File.Copy(texture, output + "/Textures/" + Path.GetFileName(texture), true);
            }
            AssetDatabase.Refresh();
            ConfigureTextures(output);
            string rigSource = sourceFolder + "/Rigs/" + actor + ".json";
            ModelRecord model = JsonUtility.FromJson<ExportDocument>(File.ReadAllText(
                File.Exists(rigSource) ? rigSource : sourceFolder + "/models.json"))
                .Models.Single(item => item.Name == actor);
            JObject gameplay = JObject.Parse(File.ReadAllText(
                "F:/MyProject/RE/EldenRing/Converted/SingleMapGameplay/source.json"));
            JObject npc = (JObject)gameplay["Parameters"]["NpcParam"].Single(row => row.Value<int>("ID") == npcId)["Fields"];
            var root = new GameObject(actor);
            try
            {
                Matrix4x4 mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
                var bones = new Transform[model.Bones.Length];
                for (int index = 0; index < bones.Length; index++)
                {
                    BoneRecord record = model.Bones[index];
                    Transform bone = new GameObject(record.Name).transform;
                    bone.SetParent(record.Parent < 0 ? root.transform : bones[record.Parent], false);
                    Quaternion rotation = new(record.Rotation[0], record.Rotation[1], record.Rotation[2], record.Rotation[3]);
                    bone.localPosition = new Vector3(-record.Position[0], record.Position[1], record.Position[2]);
                    bone.localRotation = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
                    bone.localScale = Vector(record.Scale, 0);
                    bones[index] = bone;
                }
                Material[] materials = BuildMaterials(model, output);
                for (int index = 0; index < model.Meshes.Length; index++)
                {
                    MeshRecord record = model.Meshes[index];
                    var part = new GameObject(actor + "_" + index);
                    part.transform.SetParent(root.transform, false);
                    var renderer = part.AddComponent<SkinnedMeshRenderer>();
                    var mesh = new Mesh { name = part.name, indexFormat = IndexFormat.UInt32 };
                    int count = record.Positions.Length / 3;
                    mesh.vertices = Enumerable.Range(0, count).Select(vertex =>
                        mirror.MultiplyPoint3x4(Vector(record.Positions, vertex * 3))).ToArray();
                    mesh.normals = Enumerable.Range(0, count).Select(vertex =>
                        mirror.MultiplyVector(Vector(record.Normals, vertex * 3))).ToArray();
                    mesh.uv = Enumerable.Range(0, count).Select(vertex =>
                        new Vector2(record.UV[vertex * 2], record.UV[vertex * 2 + 1])).ToArray();
                    mesh.triangles = record.Indices;
                    var weights = new BoneWeight[count];
                    for (int vertex = 0; vertex < count; vertex++)
                    {
                        int offset = vertex * 4;
                        weights[vertex] = new BoneWeight
                        {
                            boneIndex0 = record.BoneIndices[offset], weight0 = record.Weights[offset],
                            boneIndex1 = record.BoneIndices[offset + 1], weight1 = record.Weights[offset + 1],
                            boneIndex2 = record.BoneIndices[offset + 2], weight2 = record.Weights[offset + 2],
                            boneIndex3 = record.BoneIndices[offset + 3], weight3 = record.Weights[offset + 3]
                        };
                    }
                    mesh.boneWeights = weights;
                    mesh.bindposes = bones.Select(bone => bone.worldToLocalMatrix).ToArray();
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    string meshPath = output + "/Meshes/" + mesh.name + ".asset";
                    Mesh previous = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (previous == null)
                    {
                        AssetDatabase.CreateAsset(mesh, meshPath);
                    }
                    else
                    {
                        previous.Clear();
                        previous.indexFormat = mesh.indexFormat;
                        previous.vertices = mesh.vertices;
                        previous.normals = mesh.normals;
                        previous.uv = mesh.uv;
                        previous.triangles = mesh.triangles;
                        previous.boneWeights = mesh.boneWeights;
                        previous.bindposes = mesh.bindposes;
                        previous.RecalculateBounds();
                        previous.RecalculateTangents();
                        EditorUtility.SetDirty(previous);
                        UnityEngine.Object.DestroyImmediate(mesh);
                        mesh = previous;
                    }
                    renderer.sharedMesh = mesh;
                    renderer.bones = bones;
                    renderer.rootBone = bones[0];
                    renderer.sharedMaterial = materials[record.Material];
                    renderer.updateWhenOffscreen = true;
                    int mask = ParseDrawMask(model.Materials[record.Material].Name);
                    renderer.enabled = mask < 0 || npc.Value<int>("modelDispMask" + mask) == 1;
                }
                string archive = actor == "c4071" ? "c4070_div00" : actor + "_div00";
                string motion = sourceFolder + "/Actions/" + actor + "/Animations/" + archive +
                    "/a000_000000.matrixbin";
                float error;
                AnimationClip idle = ImportOriginalCreatureMotion(root, bones, motion,
                    output + "/Animations/a000_000000.anim", out error);
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(idle);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(idle, settings);
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                string controllerPath = output + "/" + actor + ".controller";
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) ??
                    AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                AnimatorStateMachine machine = controller.layers[0].stateMachine;
                AnimatorState state = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == "Idle") ??
                    machine.AddState("Idle");
                state.motion = idle;
                machine.defaultState = state;
                animator.runtimeAnimatorController = controller;
                Avatar avatar = AvatarBuilder.BuildGenericAvatar(root, "Pelvis");
                string avatarPath = output + "/" + actor + " Avatar.asset";
                Avatar previousAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
                if (previousAvatar == null)
                {
                    AssetDatabase.CreateAsset(avatar, avatarPath);
                }
                else
                {
                    EditorUtility.CopySerialized(avatar, previousAvatar);
                    UnityEngine.Object.DestroyImmediate(avatar);
                    avatar = previousAvatar;
                }
                animator.avatar = avatar;
                if (!avatar.isValid || avatar.isHuman)
                {
                    throw new InvalidDataException("Source creature must retain a valid generic rig.");
                }
                PrefabUtility.SaveAsPrefabAsset(root, output + "/" + actor + ".prefab");
                AssetDatabase.SaveAssets();
                foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(item => item.enabled))
                {
                    var baked = new Mesh();
                    try
                    {
                        renderer.BakeMesh(baked);
                        if (baked.bounds.size.magnitude > 20f || !baked.vertices.All(vertex =>
                            float.IsFinite(vertex.x) && float.IsFinite(vertex.y) && float.IsFinite(vertex.z)))
                        {
                            throw new InvalidDataException("Invalid original creature skin: " + renderer.name);
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(baked);
                    }
                }
                Capture(root, actor + "-original-idle.png", actor);
                Directory.CreateDirectory("Docs/OriginalSingleMap");
                File.WriteAllText("Docs/OriginalSingleMap/" + actor + "-rig.txt", "PASS: original generic rig; bones=" +
                    bones.Length + "; meshes=" + model.Meshes.Length + "; source NPC display masks; max world matrix error=" +
                    error + ". Idle only; combat AI and remaining motions are not installed by this step.\n");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static AnimationClip ImportOriginalCreatureMotion(GameObject root, Transform[] bones,
            string sourcePath, string assetPath, out float maximumError)
        {
            using var reader = new BinaryReader(File.OpenRead(sourcePath));
            int headerSize = reader.ReadInt32();
            if (headerSize < 1 || headerSize > 1024 * 1024)
            {
                throw new InvalidDataException("Invalid source matrix header.");
            }
            JObject header = JObject.Parse(Encoding.UTF8.GetString(reader.ReadBytes(headerSize)));
            string[] names = header["bones"].ToObject<string[]>();
            int frames = header.Value<int>("frame_count");
            float duration = header.Value<float>("duration");
            if (frames < 2 || names.Length != bones.Length || frames > 100000 ||
                !float.IsFinite(duration) || duration <= 0f ||
                reader.BaseStream.Length != 4L + headerSize + (long)frames * names.Length * 64)
            {
                throw new InvalidDataException("Source matrix dimensions do not match the original rig.");
            }
            Matrix4x4 swap = Matrix4x4.identity;
            swap.m11 = swap.m22 = 0f;
            swap.m12 = swap.m21 = 1f;
            Matrix4x4 basis = Matrix4x4.identity;
            basis.m00 = basis.m11 = 0f;
            basis.m01 = basis.m10 = 1f;
            basis.m22 = -1f;
            Matrix4x4 mirror = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
            int[] sourceIndices = names.Select(name => Array.FindIndex(bones, bone => bone.name == name)).ToArray();
            if (sourceIndices.Any(index => index < 0) || sourceIndices.Distinct().Count() != bones.Length)
            {
                throw new InvalidDataException("Source animation and FLVER bone identities differ.");
            }
            var source = new Matrix4x4[frames, bones.Length];
            for (int frame = 0; frame < frames; frame++)
            {
                for (int index = 0; index < bones.Length; index++)
                {
                    Matrix4x4 matrix = Matrix4x4.zero;
                    for (int component = 0; component < 16; component++)
                    {
                        matrix[component / 4, component % 4] = reader.ReadSingle();
                    }
                    source[frame, sourceIndices[index]] = mirror * swap * matrix * basis * swap * mirror;
                }
            }
            var clip = new AnimationClip { name = root.name + "_" + header.Value<string>("name"), frameRate = 30f };
            var bindings = new System.Collections.Generic.List<EditorCurveBinding>();
            var curves = new System.Collections.Generic.List<AnimationCurve>();
            for (int index = 0; index < bones.Length; index++)
            {
                int parent = Array.IndexOf(bones, bones[index].parent);
                string path = AnimationUtility.CalculateTransformPath(bones[index], root.transform);
                var samples = new float[10, frames];
                Quaternion previous = Quaternion.identity;
                for (int frame = 0; frame < frames; frame++)
                {
                    Matrix4x4 local = parent < 0 ? source[frame, index] : source[frame, parent].inverse * source[frame, index];
                    Quaternion rotation = Quaternion.LookRotation(local.GetColumn(2), local.GetColumn(1));
                    if (frame > 0 && Quaternion.Dot(previous, rotation) < 0f)
                    {
                        rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    }
                    previous = rotation;
                    Vector3 scale = local.lossyScale;
                    scale.x = Vector3.Dot(local.GetColumn(0), Vector3.Cross(local.GetColumn(1), local.GetColumn(2))) < 0f ?
                        -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                    Vector3 position = local.GetColumn(3);
                    float[] values = { position.x, position.y, position.z, rotation.x, rotation.y, rotation.z, rotation.w,
                        scale.x, scale.y, scale.z };
                    for (int channel = 0; channel < 10; channel++)
                    {
                        samples[channel, frame] = values[channel];
                    }
                }
                for (int channel = 0; channel < 10; channel++)
                {
                    bool constant = Enumerable.Range(1, frames - 1).All(frame =>
                        Mathf.Abs(samples[channel, frame] - samples[channel, 0]) < 0.000002f);
                    int[] selected = constant ? new[] { 0, frames - 1 } : Enumerable.Range(0, frames).ToArray();
                    float step = duration / (frames - 1);
                    var keys = new Keyframe[selected.Length];
                    for (int key = 0; key < selected.Length; key++)
                    {
                        int frame = selected[key];
                        float incoming = constant || key == 0 ? 0f :
                            (samples[channel, frame] - samples[channel, selected[key - 1]]) /
                            ((frame - selected[key - 1]) * step);
                        float outgoing = constant || key == selected.Length - 1 ? 0f :
                            (samples[channel, selected[key + 1]] - samples[channel, frame]) /
                            ((selected[key + 1] - frame) * step);
                        keys[key] = new Keyframe(frame * step, samples[channel, frame], incoming, outgoing);
                    }
                    var curve = new AnimationCurve(keys);
                    string property = channel < 3 ? "m_LocalPosition." + "xyz"[channel] : channel < 7 ?
                        "m_LocalRotation." + "xyzw"[channel - 3] : "m_LocalScale." + "xyz"[channel - 7];
                    bindings.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), property));
                    curves.Add(curve);
                }
            }
            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
            clip.EnsureQuaternionContinuity();
            maximumError = 0f;
            for (int frame = 0; frame < frames; frame++)
            {
                clip.SampleAnimation(root, frame * duration / (frames - 1));
                for (int index = 0; index < bones.Length; index++)
                {
                    Matrix4x4 actual = root.transform.worldToLocalMatrix * bones[index].localToWorldMatrix;
                    for (int component = 0; component < 16; component++)
                    {
                        maximumError = Mathf.Max(maximumError, Mathf.Abs(actual[component] - source[frame, index][component]));
                    }
                }
            }
            if (!float.IsFinite(maximumError) || maximumError > 0.0002f)
            {
                UnityEngine.Object.DestroyImmediate(clip);
                throw new InvalidDataException("Source creature matrix reconstruction error: " + maximumError);
            }
            AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(clip, assetPath);
                return clip;
            }
            EditorUtility.CopySerialized(clip, saved);
            UnityEngine.Object.DestroyImmediate(clip);
            return saved;
        }
    }
}
