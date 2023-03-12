using UnityEngine;
using System.Runtime.InteropServices;
using MelonLoader;

namespace Kafe.CVRSuperMario64;

public static class MarioExtensions {

    public static Vector3 ToMarioRotation(this Vector3 rot) {

        float Fmod(float a, float b) {
            return a - b * Mathf.Floor(a / b);
        }

        float FixAngle(float a) {
            return Fmod(a + 180.0f, 360.0f) - 180.0f;
        }

        return new Vector3(FixAngle(-rot.x), FixAngle(rot.y), FixAngle(rot.z));
    }

    public static Vector3 ToMarioPosition(this Vector3 pos) {
        return Interop.SCALE_FACTOR * Vector3.Scale(pos, new Vector3(-1, 1, 1));
    }
}

internal static class Interop {

    public const float SCALE_FACTOR = 1000.0f;
    //public const float SCALE_FACTOR = 100.0f;

    public const int SM64_TEXTURE_WIDTH = 64 * 11;
    public const int SM64_TEXTURE_HEIGHT = 64;
    public const int SM64_GEO_MAX_TRIANGLES = 1024;

    public const float SM64_HEALTH_PER_HEALTH_POINT = 0x100;

    public const byte SECONDS_MULTIPLIER = 40;

    public const int SM64_LEVEL_RESET_VALUE = -10000;

    // It seems a collider can't be too big, otherwise it will be ignored
    // This seems like too much of a pain to fix rn, let the future me worry about it
    public const int SM64_MAX_VERTEX_DISTANCE = 250000 * (int) SCALE_FACTOR;

    public const float SM64_DEG2ANGLE = 182.04459f;

    // Was having weird crashes, I think it's due the audio ticks and mario ticks being called from diff threads
    private static readonly object Lock = new();

    private static readonly ushort[] MUSICS = {
        (ushort) MusicSeqId.SEQ_MENU_TITLE_SCREEN,
        (ushort) (MusicSeqId.SEQ_MENU_TITLE_SCREEN | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_LEVEL_GRASS,
        (ushort) (MusicSeqId.SEQ_LEVEL_GRASS | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_LEVEL_INSIDE_CASTLE,
        (ushort) (MusicSeqId.SEQ_LEVEL_INSIDE_CASTLE | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_LEVEL_WATER,
        (ushort) (MusicSeqId.SEQ_LEVEL_WATER | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_LEVEL_HOT,
        (ushort) (MusicSeqId.SEQ_LEVEL_HOT | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_LEVEL_BOSS_KOOPA,
        (ushort) (MusicSeqId.SEQ_LEVEL_BOSS_KOOPA | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_LEVEL_SNOW,
        (ushort) (MusicSeqId.SEQ_LEVEL_SNOW | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_LEVEL_SLIDE,
        (ushort) (MusicSeqId.SEQ_LEVEL_SLIDE | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_LEVEL_SPOOKY,
        (ushort) (MusicSeqId.SEQ_LEVEL_SPOOKY | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_EVENT_POWERUP,
        (ushort) (MusicSeqId.SEQ_EVENT_POWERUP | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_EVENT_METAL_CAP,
        (ushort) (MusicSeqId.SEQ_EVENT_METAL_CAP | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_LEVEL_KOOPA_ROAD,
        (ushort) (MusicSeqId.SEQ_LEVEL_KOOPA_ROAD | MusicSeqId.SEQ_VARIATION),

        (ushort) MusicSeqId.SEQ_LEVEL_BOSS_KOOPA_FINAL,
        (ushort) (MusicSeqId.SEQ_LEVEL_BOSS_KOOPA_FINAL | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_MENU_FILE_SELECT,
        (ushort) (MusicSeqId.SEQ_MENU_FILE_SELECT | MusicSeqId.SEQ_VARIATION),
        (ushort) MusicSeqId.SEQ_EVENT_CUTSCENE_CREDITS,
        (ushort) (MusicSeqId.SEQ_EVENT_CUTSCENE_CREDITS | MusicSeqId.SEQ_VARIATION),
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct SM64Surface {
        public short type;
        public short force;
        public ushort terrain;
        public int v0x, v0y, v0z;
        public int v1x, v1y, v1z;
        public int v2x, v2y, v2z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SM64MarioInputs {
        public float camLookX, camLookZ;
        public float stickX, stickY;
        public byte buttonA, buttonB, buttonZ;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SM64MarioState {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] position;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] velocity;

        public float faceAngle;
        public short health;

        public uint action;
        public uint flags;
        public uint particleFlags;
        public short invincTimer;

        public Vector3 UnityPosition =>
            position != null
                ? new Vector3(-position[0], position[1], position[2]) / SCALE_FACTOR
                : Vector3.zero;

        public Quaternion UnityRotation => Quaternion.Euler(0f, Mathf.Repeat((-Mathf.Rad2Deg * faceAngle) + 180f, 360f) - 180f, 0f);

        public float HealthPoints => health / SM64_HEALTH_PER_HEALTH_POINT;

        public bool IsAttacking() => (action & (uint) ActionFlags.ACT_FLAG_ATTACKING) != 0;
        public bool IsFlyingOrSwimming() => (action & (uint) ActionFlags.ACT_FLAG_SWIMMING_OR_FLYING) != 0;
        public bool IsSwimming() => (action & (uint) ActionFlags.ACT_FLAG_SWIMMING) != 0;
        public bool IsFlying() => (action & (uint) ActionFlags.ACT_FLYING) != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SM64MarioGeometryBuffers {
        public IntPtr position;
        public IntPtr normal;
        public IntPtr color;
        public IntPtr uv;
        public ushort numTrianglesUsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SM64ObjectTransform {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        private float[] position;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        private float[] eulerRotation;

        public static SM64ObjectTransform FromUnityWorld(Vector3 position, Quaternion rotation) {
            float[] VecToArr(Vector3 v) {
                return new float[] { v.x, v.y, v.z };
            }

            return new SM64ObjectTransform {
                position = VecToArr(position.ToMarioPosition()),
                eulerRotation = VecToArr(rotation.eulerAngles.ToMarioRotation())
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SM64SurfaceObject {
        public SM64ObjectTransform transform;
        public uint surfaceCount;
        public IntPtr surfaces;
    }

    [DllImport("sm64")]
    static extern void sm64_register_debug_print_function(IntPtr debugPrintFunctionPtr);

    [DllImport("sm64")]
    static extern void sm64_global_init(IntPtr rom, IntPtr outTexture);

    [DllImport("sm64")]
    static extern void sm64_global_terminate();


    [DllImport("sm64")]
    static extern void sm64_set_mario_position(uint marioId, float x, float y, float z);

    [DllImport("sm64")]
    static extern void sm64_set_mario_angle(uint marioId, float x, float y, float z);

    [DllImport("sm64")]
    static extern void sm64_set_mario_faceangle(uint marioId, float y);


    [DllImport("sm64")]
    static extern void sm64_audio_init(IntPtr rom);

    [DllImport("sm64")]
    static extern uint sm64_audio_tick(uint numQueuedSamples, uint numDesiredSamples, IntPtr audio_buffer);

    [DllImport("sm64")]
    static extern void sm64_play_music(byte player, ushort seqArgs, ushort fadeTimer);

    [DllImport("sm64")]
    static extern ushort sm64_get_current_background_music();

    [DllImport("sm64")]
    static extern void sm64_stop_background_music(ushort seqId);

    [DllImport("sm64")]
    static extern void sm64_play_sound_global(int soundBits);

    [DllImport("sm64")]
    static extern void sm64_play_sound(int soundBits, IntPtr pos);



    [DllImport("sm64")]
    static extern void sm64_set_mario_water_level(uint marioId, int level);

    [DllImport("sm64")]
    static extern void sm64_set_mario_gas_level(uint marioId, int level);



    [DllImport("sm64")]
    static extern void sm64_mario_interact_cap(uint marioId, uint capFlag, ushort capTime, byte playMusic);

    [DllImport("sm64")]
    static extern void sm64_mario_extend_cap(uint marioId, ushort capTime);



    [DllImport("sm64")]
    static extern void sm64_set_mario_state(uint marioId, uint flags);


    [DllImport("sm64")]
    static extern void sm64_set_mario_action(uint marioId, uint action);

    [DllImport("sm64")]
    static extern void sm64_set_mario_action(uint marioId, uint action, uint actionArg);


    [DllImport("sm64")]
    static extern void sm64_set_mario_invincibility(uint marioId, short timer);


    [DllImport("sm64")]
    static extern void sm64_set_mario_velocity(uint marioId, float x, float y, float z);

    [DllImport("sm64")]
    static extern void sm64_set_mario_forward_velocity(uint marioId, float vel);


    [DllImport("sm64")]
    static extern void sm64_mario_take_damage(uint marioId, uint damage, uint subtype, float x, float y, float z);

    [DllImport("sm64")]
    static extern void sm64_mario_attack(uint marioId, float x, float y, float z, float hitboxHeight);


    [DllImport("sm64")]
    static extern void sm64_mario_heal(uint marioId, byte healCounter);

    [DllImport("sm64")]
    static extern void sm64_set_mario_health(uint marioId, ushort health);


    [DllImport("sm64")]
    static extern void sm64_mario_kill(uint marioId);




    [DllImport("sm64")]
    static extern void sm64_static_surfaces_load(SM64Surface[] surfaces, ulong numSurfaces);

    [DllImport("sm64")]
    static extern uint sm64_mario_create(float marioX, float marioY, float marioZ);

    [DllImport("sm64")]
    static extern void sm64_mario_tick(uint marioId, ref SM64MarioInputs inputs, ref SM64MarioState outState, ref SM64MarioGeometryBuffers outBuffers);

    [DllImport("sm64")]
    static extern void sm64_mario_delete(uint marioId);

    [DllImport("sm64")]
    static extern uint sm64_surface_object_create(ref SM64SurfaceObject surfaceObject);

    [DllImport("sm64")]
    static extern void sm64_surface_object_move(uint objectId, ref SM64ObjectTransform transform);

    [DllImport("sm64")]
    static extern void sm64_surface_object_delete(uint objectId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    delegate void DebugPrintFuncDelegate(string str);

    public static Texture2D marioTexture { get; private set; }
    public static bool isGlobalInit { get; private set; }

    static void DebugPrintCallback(string str) {
        MelonLogger.Msg($"[libsm64] {str}");
    }

    public static void GlobalInit(byte[] rom) {
        var romHandle = GCHandle.Alloc(rom, GCHandleType.Pinned);
        var textureData = new byte[4 * SM64_TEXTURE_WIDTH * SM64_TEXTURE_HEIGHT];
        var textureDataHandle = GCHandle.Alloc(textureData, GCHandleType.Pinned);

        sm64_global_init(romHandle.AddrOfPinnedObject(), textureDataHandle.AddrOfPinnedObject());
        sm64_audio_init(romHandle.AddrOfPinnedObject());

        // With audio this has became waaaay too spammy ;_;
        // #if DEBUG
        // var callbackDelegate = new DebugPrintFuncDelegate(DebugPrintCallback);
        // sm64_register_debug_print_function(Marshal.GetFunctionPointerForDelegate(callbackDelegate));
        // #endif

        var cols = new Color32[SM64_TEXTURE_WIDTH * SM64_TEXTURE_HEIGHT];
        marioTexture = new Texture2D(SM64_TEXTURE_WIDTH, SM64_TEXTURE_HEIGHT);
        for (var ix = 0; ix < SM64_TEXTURE_WIDTH; ix++)
        for (var iy = 0; iy < SM64_TEXTURE_HEIGHT; iy++) {
            var color = new Color32(
                textureData[4 * (ix + SM64_TEXTURE_WIDTH * iy) + 0],
                textureData[4 * (ix + SM64_TEXTURE_WIDTH * iy) + 1],
                textureData[4 * (ix + SM64_TEXTURE_WIDTH * iy) + 2],
                textureData[4 * (ix + SM64_TEXTURE_WIDTH * iy) + 3]
            );
            // Make the 100% transparent colors white. So we can multiply with the vertex colors.
            if (color.a == 0) {
                color.r = 1;
                color.g = 1;
                color.b = 1;
            }
            cols[ix + SM64_TEXTURE_WIDTH * iy] = color;
        }

        marioTexture.SetPixels32(cols);
        marioTexture.Apply();

        romHandle.Free();
        textureDataHandle.Free();

        isGlobalInit = true;
    }

    public static void GlobalTerminate() {
        lock (Lock) {
            StopMusic();
            sm64_global_terminate();
            marioTexture = null;
            isGlobalInit = false;
        }
    }

    public static void PlayRandomMusic() {
        lock (Lock) {
            StopMusic();
            sm64_play_music(0, MUSICS[UnityEngine.Random.Range (0, MUSICS.Length)], 0);
        }
    }

    public static void StopMusic() {
        // Stop all music that was queued
        lock (Lock) {
            while (sm64_get_current_background_music() is var currentMusic && currentMusic != (ushort) MusicSeqId.SEQ_NONE) {
                sm64_stop_background_music(currentMusic);
            }
        }
    }

    public static void StaticSurfacesLoad(SM64Surface[] surfaces) {
        MelonLogger.Msg("Reloading all static collider surfaces, this can be caused by the Game Engine " +
                        "Initialized/Destroyed or some component with static colliders was loaded/deleted. " +
                        $"You might notice some lag spike... Total Polygons: {surfaces.Length}");
        sm64_static_surfaces_load(surfaces, (ulong)surfaces.Length);
    }

    public static uint MarioCreate(Vector3 marioPos) {
        return sm64_mario_create(marioPos.x, marioPos.y, marioPos.z);
    }

    public static SM64MarioState MarioTick(uint marioId, SM64MarioInputs inputs, Vector3[] positionBuffer,
        Vector3[] normalBuffer, Vector3[] colorBuffer, Vector2[] uvBuffer, out ushort numTrianglesUsed) {
        var outState = new SM64MarioState();

        var posHandle = GCHandle.Alloc(positionBuffer, GCHandleType.Pinned);
        var normHandle = GCHandle.Alloc(normalBuffer, GCHandleType.Pinned);
        var colorHandle = GCHandle.Alloc(colorBuffer, GCHandleType.Pinned);
        var uvHandle = GCHandle.Alloc(uvBuffer, GCHandleType.Pinned);

        var buff = new SM64MarioGeometryBuffers {
            position = posHandle.AddrOfPinnedObject(),
            normal = normHandle.AddrOfPinnedObject(),
            color = colorHandle.AddrOfPinnedObject(),
            uv = uvHandle.AddrOfPinnedObject()
        };

        lock (Lock) {
            sm64_mario_tick(marioId, ref inputs, ref outState, ref buff);
        }

        numTrianglesUsed = buff.numTrianglesUsed;

        posHandle.Free();
        normHandle.Free();
        colorHandle.Free();
        uvHandle.Free();

        return outState;
    }

    public static uint AudioTick(short[] audioBuffer, uint numDesiredSamples, uint numQueuedSamples = 0) {
        lock (Lock) {
            var audioBufferPointer = GCHandle.Alloc(audioBuffer, GCHandleType.Pinned);
            var numSamples = sm64_audio_tick(numQueuedSamples, numDesiredSamples, audioBufferPointer.AddrOfPinnedObject());
            audioBufferPointer.Free();
            return numSamples;
        }
    }

    public static void PlaySoundGlobal(SoundBitsKeys soundBitsKey) {
        lock (Lock) {
            sm64_play_sound_global((int) Utils.SoundBits[soundBitsKey]);
        }
    }

    public static void PlaySound(SoundBitsKeys soundBitsKey, Vector3 unityPosition) {
        var marioPos = unityPosition.ToMarioPosition();
        var position = new float[] { marioPos.x, marioPos.y, marioPos.z };
        var posPointer = GCHandle.Alloc(position, GCHandleType.Pinned);
        lock (Lock) {
            sm64_play_sound((int) Utils.SoundBits[soundBitsKey], posPointer.AddrOfPinnedObject());
        }
        posPointer.Free();
    }

    public static void MarioDelete(uint marioId) {
        sm64_mario_delete(marioId);
    }

    public static void MarioTakeDamage(uint marioId, Vector3 unityPosition, uint damage) {
        var marioPos = unityPosition.ToMarioPosition();
        sm64_mario_take_damage(marioId, damage, 0, marioPos.x, marioPos.y, marioPos.z);
    }

    public static void MarioSetVelocity(uint marioId, SM64MarioState previousState, SM64MarioState currentState) {
        sm64_set_mario_velocity(marioId,
            currentState.position[0] - previousState.position[0],
            currentState.position[1] - previousState.position[1],
            currentState.position[2] - previousState.position[2]);
    }

    public static void MarioSetVelocity(uint marioId, Vector3 unityVelocity) {
        var marioVelocity = unityVelocity.ToMarioPosition();
        sm64_set_mario_velocity(marioId, marioVelocity.x, marioVelocity.y, marioVelocity.z);
    }

    public static void MarioSetForwardVelocity(uint marioId, float unityVelocity) {
        sm64_set_mario_forward_velocity(marioId, unityVelocity * SCALE_FACTOR);
    }

    public static void CreateAndAppendSurfaces(List<SM64Surface> outSurfaces, int[] triangles, Vector3[] vertices, SM64SurfaceType surfaceType, SM64TerrainType terrainType) {
        for (var i = 0; i <  triangles.Length; i += 3) {
            outSurfaces.Add(new SM64Surface {
                force = 0,
                type = (short)surfaceType,
                terrain = (ushort)terrainType,
                v0x = (int) (SCALE_FACTOR * - vertices[ triangles[i    ]].x),
                v0y = (int) (SCALE_FACTOR *   vertices[ triangles[i    ]].y),
                v0z = (int) (SCALE_FACTOR *   vertices[ triangles[i    ]].z),
                v1x = (int) (SCALE_FACTOR * - vertices[ triangles[i + 2]].x),
                v1y = (int) (SCALE_FACTOR *   vertices[ triangles[i + 2]].y),
                v1z = (int) (SCALE_FACTOR *   vertices[ triangles[i + 2]].z),
                v2x = (int) (SCALE_FACTOR * - vertices[ triangles[i + 1]].x),
                v2y = (int) (SCALE_FACTOR *   vertices[ triangles[i + 1]].y),
                v2z = (int) (SCALE_FACTOR *   vertices[ triangles[i + 1]].z),
            });
        }
    }

    public static uint SurfaceObjectCreate(Vector3 position, Quaternion rotation, SM64Surface[] surfaces) {
        var surfListHandle = GCHandle.Alloc(surfaces, GCHandleType.Pinned);
        var t = SM64ObjectTransform.FromUnityWorld(position, rotation);

        SM64SurfaceObject surfObj = new SM64SurfaceObject {
            transform = t,
            surfaceCount = (uint)surfaces.Length,
            surfaces = surfListHandle.AddrOfPinnedObject()
        };

        uint result = sm64_surface_object_create(ref surfObj);

        surfListHandle.Free();

        return result;
    }

    public static void SurfaceObjectMove(uint id, Vector3 position, Quaternion rotation) {
        var t = SM64ObjectTransform.FromUnityWorld(position, rotation);
        sm64_surface_object_move(id, ref t);
    }

    public static void SurfaceObjectDelete(uint id) {
        sm64_surface_object_delete(id);
    }

    public static void MarioCap(uint marioId, CapFlags capFlags, float durationSeconds, bool playCapMusic) {
        sm64_mario_interact_cap(marioId, (uint)capFlags, (ushort)(durationSeconds * SECONDS_MULTIPLIER), playCapMusic ? (byte) 1 : (byte) 0);
    }

    public static void MarioCapExtend(uint marioId, float durationSeconds) {
        sm64_mario_extend_cap(marioId, (ushort)(durationSeconds * SECONDS_MULTIPLIER));
    }

    public static void SetLevelModifier(uint marioId, CVRSM64LevelModifier.ModifierType modifierType, float unityLevelY) {
        var marioYValue = (int)(SCALE_FACTOR * unityLevelY);
        if (Mathf.Approximately(unityLevelY, float.MinValue)) {
            marioYValue = (int)(-2500*SCALE_FACTOR);
        }
        switch (modifierType) {
            // Unity Y (height) world coord, which will be filled with water/gas
            case CVRSM64LevelModifier.ModifierType.Water:
                sm64_set_mario_water_level(marioId, marioYValue);
                break;
            case CVRSM64LevelModifier.ModifierType.Gas:
                sm64_set_mario_gas_level(marioId, marioYValue);
                break;
        }
    }

    public static void MarioSetPosition(uint marioId, Vector3 pos) {
        var marioPos = pos.ToMarioPosition();
        sm64_set_mario_position(marioId, marioPos.x, marioPos.y, marioPos.z);
    }

    public static void MarioSetFaceAngle(uint marioId, Quaternion rot) {
        var angleInDegrees = rot.eulerAngles.y;
        if (angleInDegrees > 180f) {
            angleInDegrees -= 360f;
        }
        sm64_set_mario_faceangle(marioId, -Mathf.Deg2Rad * angleInDegrees);
    }

    public static void MarioSetRotation(uint marioId, Quaternion rotation) {
        var marioRotation = rotation.eulerAngles.ToMarioRotation();
        sm64_set_mario_angle(marioId, marioRotation.x, marioRotation.y, marioRotation.z);
    }

    public static void MarioSetHealthPoints(uint marioId, float healthPoints) {
        sm64_set_mario_health(marioId, (ushort) (healthPoints * SM64_HEALTH_PER_HEALTH_POINT));
    }

    public static void MarioHeal(uint marioId, byte healthPoints) {
        // It was healing 0.25 with 1, so we multiplied by 4 EZ FIX
        sm64_mario_heal(marioId, (byte)(healthPoints*4));
    }

    public static void MarioSetAction(uint marioId, ActionFlags actionFlags) {
        sm64_set_mario_action(marioId, (uint) actionFlags);
    }

    public static void MarioSetAction(uint marioId, uint actionFlags) {
        sm64_set_mario_action(marioId, actionFlags);
    }

    public static void MarioSetState(uint marioId, uint flags) {
        sm64_set_mario_state(marioId, flags);
    }
}
