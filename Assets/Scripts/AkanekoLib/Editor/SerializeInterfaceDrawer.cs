using UnityEditor;
using UnityEngine;
using System;

/// <summary>
/// インターフェースをインスペクターからアタッチするためのクラス
/// インスペクターに「SerializeInterface<>」クラスを表示できるようにするクラス
/// 
/// CustomPropertyDrawerとかPropertyDrawerとか今の自分には未習得の技術が用いられてる。要学習
/// 
/// </summary>
[CustomPropertyDrawer(typeof(SerializeInterface<>), true)]
public class SerializeInterfaceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty referenceProperty = property.FindPropertyRelative("reference");

        // フィールドの型 (Ihoge など)
        Type fieldType = fieldInfo.FieldType.GetGenericArguments()[0];

        EditorGUI.BeginProperty(position, label, property);
        referenceProperty.objectReferenceValue = EditorGUI.ObjectField(position, label, referenceProperty.objectReferenceValue, typeof(MonoBehaviour), true);
        
        // 選択したオブジェクトが正しいインターフェースを持っているか確認
        if (referenceProperty.objectReferenceValue != null)
        {
            MonoBehaviour mb = referenceProperty.objectReferenceValue as MonoBehaviour;
            if (mb == null || !fieldType.IsAssignableFrom(mb.GetType()))
            {
                referenceProperty.objectReferenceValue = null;
                Debug.LogWarning($"選択したオブジェクトは {fieldType.Name} を実装していません。");
            }
        }

        EditorGUI.EndProperty();
    }
}
