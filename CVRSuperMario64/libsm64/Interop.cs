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

    public const float SM64_HEALTH_PER_LIFE = 0x100;

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

        public float Lives => health / SM64_HEALTH_PER_LIFE;
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
    static extern void sm64_set_mario_faceangle(uint marioId, float y);

    [DllImport("sm64")]
    static extern void sm64_set_mario_health(uint marioId, ushort health);


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
    static extern void sm64_set_mario_water_level(uint marioId, int level);

    [DllImport("sm64")]
    static extern void sm64_set_mario_gas_level(uint marioId, int level);



    [DllImport("sm64")]
    static extern void sm64_mario_interact_cap(uint marioId, uint capFlag, ushort capTime, byte playMusic);


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


        Color32[] cols = new Color32[SM64_TEXTURE_WIDTH * SM64_TEXTURE_HEIGHT];
        marioTexture = new Texture2D(SM64_TEXTURE_WIDTH, SM64_TEXTURE_HEIGHT);
        for (int ix = 0; ix < SM64_TEXTURE_WIDTH; ix++)
        for (int iy = 0; iy < SM64_TEXTURE_HEIGHT; iy++) {
            cols[ix + SM64_TEXTURE_WIDTH * iy] = new Color32(
                textureData[4 * (ix + SM64_TEXTURE_WIDTH * iy) + 0],
                textureData[4 * (ix + SM64_TEXTURE_WIDTH * iy) + 1],
                textureData[4 * (ix + SM64_TEXTURE_WIDTH * iy) + 2],
                textureData[4 * (ix + SM64_TEXTURE_WIDTH * iy) + 3]
            );
        }

        marioTexture.SetPixels32(cols);
        marioTexture.Apply();

        romHandle.Free();
        textureDataHandle.Free();

        isGlobalInit = true;
    }

    public static void GlobalTerminate() {
        sm64_global_terminate();
        StopMusic();
        marioTexture = null;
        isGlobalInit = false;
    }

    public static void PlayRandomMusic() {
        StopMusic();
        sm64_play_music(0, MUSICS[UnityEngine.Random.Range (0, MUSICS.Length)], 0);
    }

    public static void StopMusic() {
        // Stop all music that was queued
        while (sm64_get_current_background_music() is var currentMusic && currentMusic != (ushort) MusicSeqId.SEQ_NONE) {
            sm64_stop_background_music(currentMusic);
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
        Vector3[] normalBuffer, Vector3[] colorBuffer, Vector2[] uvBuffer) {
        SM64MarioState outState = new SM64MarioState();

        var posHandle = GCHandle.Alloc(positionBuffer, GCHandleType.Pinned);
        var normHandle = GCHandle.Alloc(normalBuffer, GCHandleType.Pinned);
        var colorHandle = GCHandle.Alloc(colorBuffer, GCHandleType.Pinned);
        var uvHandle = GCHandle.Alloc(uvBuffer, GCHandleType.Pinned);

        SM64MarioGeometryBuffers buff = new SM64MarioGeometryBuffers {
            position = posHandle.AddrOfPinnedObject(),
            normal = normHandle.AddrOfPinnedObject(),
            color = colorHandle.AddrOfPinnedObject(),
            uv = uvHandle.AddrOfPinnedObject()
        };

        sm64_mario_tick(marioId, ref inputs, ref outState, ref buff);

        posHandle.Free();
        normHandle.Free();
        colorHandle.Free();
        uvHandle.Free();

        return outState;
    }

    public static uint AudioTick(short[] audioBuffer, uint numDesiredSamples, uint numQueuedSamples = 0) {
        var audioBufferPointer = GCHandle.Alloc(audioBuffer, GCHandleType.Pinned);
        var numSamples = sm64_audio_tick(numQueuedSamples, numDesiredSamples, audioBufferPointer.AddrOfPinnedObject());
        audioBufferPointer.Free();
        return numSamples;
    }

    public static void MarioDelete(uint marioId) {
        sm64_mario_delete(marioId);
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

    public static void MarioCap(uint marioId, CapFlags capFlags, ushort capTime = 0, bool playCapMusic = true) {
        // Untested (seems broken)
        sm64_mario_interact_cap(marioId, (uint)capFlags, capTime, playCapMusic ? (byte) 1 : (byte) 0);
    }

    public static void SetWaterLevel(uint marioId, float waterLevelY) {
        // Unity Y (height) world coord, which will be filled with water
        sm64_set_mario_water_level(marioId, (int) (SCALE_FACTOR * waterLevelY));
    }

    public static void SetGasLevel(uint marioId, float gasLevelY) {
        // Unity Y (height) world coord, which will be filled with gas
        sm64_set_mario_gas_level(marioId, (int) (SCALE_FACTOR * gasLevelY));
    }

    public static void MarioSetPosition(uint marioId, Vector3 pos) {
        var marioPos = pos.ToMarioPosition();
        sm64_set_mario_position(marioId, marioPos.x, marioPos.y, marioPos.z);
    }

    public static void MarioSetRotation(uint marioId, Quaternion rot) {
        var angleInDegrees = rot.eulerAngles.y;
        if (angleInDegrees > 180f) {
            angleInDegrees -= 360f;
        }
        sm64_set_mario_faceangle(marioId, -Mathf.Deg2Rad * angleInDegrees);
    }

    public static void MarioSetLives(uint marioId, float lives) {
        sm64_set_mario_health(marioId, (ushort) (lives * SM64_HEALTH_PER_LIFE));
    }
}
