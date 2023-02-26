namespace Kafe.CVRSuperMario64;

public enum SM64TerrainType {
    Grass = 0x0000,
    Stone = 0x0001,
    Snow = 0x0002,
    Sand = 0x0003,
    Spooky = 0x0004,
    Water = 0x0005,
    Slide = 0x0006,
}

public enum SM64SurfaceType {
    Default = 0x0000, // Environment default
    Burning = 0x0001, // Lava / Frostbite (in SL), but is used mostly for Lava
    Hangable = 0x0005, // Ceiling that Mario can climb on
    Slow = 0x0009, // Slow down Mario, unused
    VerySlippery = 0x0013, // Very slippery, mostly used for slides
    Slippery = 0x0014, // Slippery
    NotSlippery = 0x0015, // Non-slippery, climbable
    ShallowQuicksand = 0x0021, // Shallow Quicksand (depth of 10 units)
    DeepQuicksand = 0x0022, // Quicksand (lethal, slow, depth of 160 units)
    InstantQuicksand = 0x0023, // Quicksand (lethal, instant)
    Ice = 0x002E, // Slippery Ice, in snow levels and THI's water floor
    Hard = 0x0030, // Hard floor (Always has fall damage)
    HardSlippery = 0x0035, // Hard and slippery (Always has fall damage)
    HardVerySlippery = 0x0036, // Hard and very slippery (Always has fall damage)
    HardNotSlippery = 0x0037, // Hard and Non-slippery (Always has fall damage)
    VerticalWind = 0x0038, // Death at bottom with vertical wind
}

[Flags]
public enum MusicSeqId {
    SEQ_SOUND_PLAYER                = 0x00,
    SEQ_EVENT_CUTSCENE_COLLECT_STAR = 0x01,
    SEQ_MENU_TITLE_SCREEN           = 0x02,
    SEQ_LEVEL_GRASS                 = 0x03,
    SEQ_LEVEL_INSIDE_CASTLE         = 0x04,
    SEQ_LEVEL_WATER                 = 0x05,
    SEQ_LEVEL_HOT                   = 0x06,
    SEQ_LEVEL_BOSS_KOOPA            = 0x07,
    SEQ_LEVEL_SNOW                  = 0x08,
    SEQ_LEVEL_SLIDE                 = 0x09,
    SEQ_LEVEL_SPOOKY                = 0x0A,
    SEQ_EVENT_PIRANHA_PLANT         = 0x0B,
    SEQ_LEVEL_UNDERGROUND           = 0x0C,
    SEQ_MENU_STAR_SELECT            = 0x0D,
    SEQ_EVENT_POWERUP               = 0x0E,
    SEQ_EVENT_METAL_CAP             = 0x0F,
    SEQ_EVENT_KOOPA_MESSAGE         = 0x10,
    SEQ_LEVEL_KOOPA_ROAD            = 0x11,
    SEQ_EVENT_HIGH_SCORE            = 0x12,
    SEQ_EVENT_MERRY_GO_ROUND        = 0x13,
    SEQ_EVENT_RACE                  = 0x14,
    SEQ_EVENT_CUTSCENE_STAR_SPAWN   = 0x15,
    SEQ_EVENT_BOSS                  = 0x16,
    SEQ_EVENT_CUTSCENE_COLLECT_KEY  = 0x17,
    SEQ_EVENT_ENDLESS_STAIRS        = 0x18,
    SEQ_LEVEL_BOSS_KOOPA_FINAL      = 0x19,
    SEQ_EVENT_CUTSCENE_CREDITS      = 0x1A,
    SEQ_EVENT_SOLVE_PUZZLE          = 0x1B,
    SEQ_EVENT_TOAD_MESSAGE          = 0x1C,
    SEQ_EVENT_PEACH_MESSAGE         = 0x1D,
    SEQ_EVENT_CUTSCENE_INTRO        = 0x1E,
    SEQ_EVENT_CUTSCENE_VICTORY      = 0x1F,
    SEQ_EVENT_CUTSCENE_ENDING       = 0x20,
    SEQ_MENU_FILE_SELECT            = 0x21,
    SEQ_EVENT_CUTSCENE_LAKITU       = 0x22, // (not in JP),
    SEQ_VARIATION                   = 0x80, // Arg, should be used with | on other sequences
    SEQ_NONE                        = 0xFFFF, // When no current music is being played it will return this
}

[Flags]
public enum CapFlags {
    MARIO_NORMAL_CAP                = 0x00000001,
    MARIO_VANISH_CAP                = 0x00000002,
    MARIO_METAL_CAP                 = 0x00000004,
    MARIO_WING_CAP                  = 0x00000008,
    // MARIO_CAP_ON_HEAD               = 0x00000010,
    // MARIO_CAP_IN_HAND               = 0x00000020,
}

[Flags]
public enum ActionFlags {
    ACT_FLAG_INVULNERABLE           = 0x00020000,
    ACT_FLAG_ATTACKING              = 0x00800000,
    ACT_FLAG_IDLE                   = 0x00400000,
    ACT_FLAG_SHORT_HITBOX           = 0x00008000,
}
