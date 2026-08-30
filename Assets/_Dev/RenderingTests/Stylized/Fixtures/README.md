# Stylized Rendering Test — Fixtures

The test scene `SCN_StylizedRendering_Test.unity` builds its fixtures from Unity
primitives + the validation materials under `../Materials/`:

| Fixture | Built from | Material |
| --- | --- | --- |
| Character Fixture | Capsule (scaled 0.9 × 1.6 × 0.9) | M_TEST_Toon_Character (Player base map) |
| Weapon Fixture | Cube (0.15 × 1.4 × 0.15, rotated 35°) | M_TEST_Toon_Weapon (Claymore base map) |
| Sphere / Cube | Sphere / Cube primitives | M_TEST_Toon_Sphere / M_TEST_Toon_Cube |
| Floor / Wall | Scaled cubes | M_TEST_Toon_Floor / M_TEST_Toon_Wall |
| Emissive Object | Sphere (HDR emissive) | M_TEST_Toon_Emissive |

Notes:
- The character fixture is a primitive stand-in for outline validation on
  MeshRenderer + multi-slot materials. To validate SkinnedMeshRenderer outlines
  with a real character, place the `Player.prefab` (or any skinned prefab with a
  Toon material) into the scene — the outline pass handles CPU-skinned meshes.
- The weapon fixture mirrors the Straight Sword proportions; the real
  `Straight Sword.prefab` can be dropped in for the skinned/multi-material test.
