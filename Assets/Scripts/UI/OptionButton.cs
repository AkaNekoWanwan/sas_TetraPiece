using UnityEngine;
using UnityEngine.UI;
using AkanekoLib;
using UnityEngine.Events;

[DefaultExecutionOrder(100)]
public class OptionButton : MonoBehaviour
{
    public Image buttonBack;
    public Image image;
    public Sprite onSprite;
    public Sprite offSprite;
    public string optionKey = "OptionSetting";
    public CustomButton customButton;
    public event UnityAction<bool> onOptionChanged;

    private void Start()
    {
        bool isOn = PlayerPrefs.GetInt(optionKey, 1) == 1;
        SetOption(isOn);
        customButton.onClick += ToggleOption;
    }
    public void ToggleOption()
    {
        bool isOn = PlayerPrefs.GetInt(optionKey, 1) == 1;
        SetOption(!isOn);
    }
    public void UpdateView()
    {
        bool isOn = PlayerPrefs.GetInt(optionKey, 1) == 1;
        SetOption(isOn);
    }
    
    public void SetOption(bool isOn)
    {
        if (isOn)
        {
            buttonBack.color = Color.white;
            image.sprite = onSprite;
            image.color = new Color32(43, 43, 43, 255);
            PlayerPrefs.SetInt(optionKey, 1);
            // onOptionChanged?.Invoke(true);
            GameDataManager.OnChangeOption(optionKey, true);
        }
        else
        {
            buttonBack.color = new Color32(172, 172, 172, 255);
            image.sprite = offSprite;
            image.color = Color.black;
            PlayerPrefs.SetInt(optionKey, 0);
            // onOptionChanged?.Invoke(false);
            GameDataManager.OnChangeOption(optionKey, false);
        }
    }
}
