using UnityEngine;

public static class GameConst
{
// #if UNITY_EDITOR
    private const bool IsCreative = true;
// #else
    // private const bool IsCreative = false;
// #endif
#if UNITY_EDITOR
    private const bool IsScreenShot = true;
#else
    private const bool IsScreenShot = false;
#endif

    public const float ADD_Y_OFFSET = 3.5f;

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
