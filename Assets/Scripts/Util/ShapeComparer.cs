using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// 関数内でVector2Intを定義するとプレイアブルでエラーを起こすため、外部に出す
public static class ShapeComparer
{
    static Vector2Int offset = Vector2Int.zero;
    static Vector2Int baseOffset = Vector2Int.zero;
    static Vector2Int translatedPoint = Vector2Int.zero;
    /// <summary>
    /// 2つの相対位置リストを比較し、平行移動によって形が一致するかを確認します。
    /// </summary>
    /// <param name="shapeA">比較対象の最初のシェイプリスト。</param>
    /// <param name="shapeB">比較対象の2番目のシェイプリスト。</param>
    /// <returns>形が一致すれば true、そうでなければ false を返します。</returns>
    public static bool CheckShapeEquality(List<Vector2Int> shapeA, List<Vector2Int> shapeB, ShapeType shapeType)
    {
        Debug.Log($"ShapeComparer:形状一致チェック:{shapeA == null}, {shapeB == null}, {shapeA.Count}, {shapeB.Count}");
        // 1. 要素数が異なる場合は、形は絶対に一致しない。
        if (shapeA == null || shapeB == null || shapeA.Count != shapeB.Count)
        {
            return false;
        }

        int count = shapeA.Count;
        if (count == 0)
        {
            // 両方空リストなら一致とみなす
            return true;
        }
        // 四角ならとりあえず無視する
        // if(shapeType == ShapeType.Square)
        // {
        //     return true;
        // }

        // 2. shapeBの要素を、オフセットを使ってハッシュセットに格納する。
        // これにより、O(1)で点の存在チェックが可能になる。
        // ただし、オフセットがまだ定まっていないため、ここではshapeBの元データを格納する。
        HashSet<Vector2Int> setB = new HashSet<Vector2Int>(shapeB);

        // 3. shapeAの最初の点 (A[0]) と shapeBの各点 (B[i]) を対応させて、
        // 可能なオフセット（移動量）を一つずつ試す。
        // オフセット = B[i] - A[0]
        for (int i = 0; i < count; i++)
        {
            baseOffset = shapeB[i] - shapeA[0];
            bool matchFound = true;

            // Debug.Log($"ShapeComparer: baseOffset->{baseOffset}");

            // 4. この仮定のオフセットを使って、shapeAの全要素を移動させてみる。
            // 移動後の点がsetBの中に全て存在するか確認する。
            for (int j = 0; j < count; j++)
            {
                offset = baseOffset;
                // 六角形の時の特殊処理
                if(shapeType == ShapeType.Hex)
                {
                    // ２点の基準座標のXの偶数奇数が異なるなら
                    if(baseOffset.x % 2 != 0)
                    {
                        // 比較中の座標と基準座標のXの偶数奇数が異なるなら、Yの比較に補正
                        if((shapeA[0].x - shapeA[j].x) % 2 != 0)
                        {
                            
                            if(shapeA[0].x % 2 == 0)
                            {
                                offset.y++;
                            }
                            else
                            {
                                offset.y--;
                            }
                        }
                    }
                }

                translatedPoint = shapeA[j] + offset;

                // shapeBの中に移動後の点が存在しなければ、このオフセットは間違い。
                if (!setB.Contains(translatedPoint))
                {
                    matchFound = false;
                }

                Debug.Log($"ShapeComparer:比較情報: {shapeType}, baseOffset:{baseOffset}, offset:{offset}, shapeA[0]:{shapeA[0]}, shapeA[{j}]:{shapeA[j]}, shapeB[{i}]:{shapeB[i]}, matchFound:{matchFound}");
                if(!matchFound)
                    break;
            }

            // 5. shapeAの全要素がsetBにマッチしたら、形は一致している。
            if (matchFound)
            {
                Debug.Log($"ShapeComparer:形状の一致を検知！");
                return true;
            }
        }

        Debug.Log($"ShapeComparer:捜査の末一致が見つかりませんでした: {shapeA}, {shapeB}");
        // どのオフセットを試しても完全な一致が見つからなかった場合。
        return false;
    }

    // マッピングを用いてより厳密に形状を比較するバージョン
    public static bool CheckShapeEquality(Dictionary<Vector2Int, Vector2Int> shapeMap, ShapeType shapeType)
    {
        List<Vector2Int> shapeA = shapeMap.Keys.ToList();
        List<Vector2Int> shapeB = shapeMap.Values.ToList();
        baseOffset = shapeB[0] - shapeA[0];

        foreach (var pair in shapeMap)
        {
            Vector2Int pointA = pair.Key;
            Vector2Int pointB = pair.Value;

            offset = baseOffset;
            // 六角形の時の特殊処理
            if (shapeType == ShapeType.Hex)
            {
                // ２点の基準座標のXの偶数奇数が異なるなら
                if (baseOffset.x % 2 != 0)
                {
                    // 比較中の座標と基準座標のXの偶数奇数が異なるなら、Yの比較に補正
                    if ((shapeA[0].x - pointA.x) % 2 != 0)
                    {

                        if (shapeA[0].x % 2 == 0)
                        {
                            offset.y++;
                        }
                        else
                        {
                            offset.y--;
                        }
                    }
                }
            }

            translatedPoint = pointA + offset;
            if (translatedPoint != pointB)
            {
                Debug.Log($"ShapeComparer:厳密比較で不一致検知: {pointA} + {offset} = {translatedPoint} != {pointB}");
                return false;
            }
        }
        return true;
    }
}