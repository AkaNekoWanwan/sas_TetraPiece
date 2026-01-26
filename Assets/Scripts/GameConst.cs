using UnityEngine;

public static class GameConst
{
// #if UNITY_EDITOR
    private const bool IsCreative = true;
// #else
    // private const bool IsCreative = false;
// #endif
#if UNITY_EDITOR
    private const bool IsScreenShot = false;
#else
    private const bool IsScreenShot = false;
#endif

    public const float ADD_Y_OFFSET = 3.5f;

    public const int FIRST_HOME_STAGE_AFTER_CLEAR = 5;   // クリア後初めてホームに戻るステージ番号

    public const float LevelFramePosY_A = -66f;
    public const float LevelFramePosY_B = -106f;

    public static bool IsCreativeMode()
    {
        // if(!Debug.isDebugBuild)
        //     return false;
        return IsCreative;
    }
    public static bool IsScreenShotMode()
    {
        if(!Debug.isDebugBuild)
            return false;
        return IsScreenShot;
    }
    // セーブデータに関係なくミュートにするか
    public static bool IsMute()
    {
        if(!Debug.isDebugBuild)
            return false;
        return IsCreativeMode();
    }
}
