using UnityEngine;

public static class GameConst
{
    private const bool IsCreative = true;

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
