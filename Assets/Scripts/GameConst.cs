using UnityEngine;

public static class GameConst
{
#if UNITY_EDITOR
    private const bool IsCreative = false;
#else
    private const bool IsCreative = false;
#endif

    public static bool IsCreativeMode()
    {
        if(!Debug.isDebugBuild)
            return false;
        return IsCreative;
    }
    // セーブデータに関係なくミュートにするか
    public static bool IsMute()
    {
        if(!Debug.isDebugBuild)
            return false;
        return IsCreativeMode();
    }
}
