using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Similar to the regular Popup, but prompts the user for a yes/no response.
/// </summary>
public class QuestionPopup : MonoBehaviour
{
    //references to other gameobjects/components
    public Text text;

    /// <summary>
    /// Closes the popup.
    /// </summary>
    /// <param name="yes">Whether the "yes" button was pressed. False if the user presses no, back, escape etc.</param>
    public void Close(bool yes = false)
    {
        gameObject.SetActive(false);
        text.text = string.Empty;
        if (Callback != null)
            Callback(yes);
    }

    public static QuestionPopup Instance;                   //static reference to the (only) question popup instance
    public static System.Action<bool> Callback = null;

    /// <summary>
    /// Opens the popup.
    /// </summary>
    /// <param name="message">The string to be displayed by the popup, preferably a yes/no question.</param>
    /// <param name="callback">An optional callback that takes a boolean argument of whether the yes button was pressed.</param>
    public static void ActivatePopup(string message, System.Action<bool> callback = null)
    {
        Instance.text.text = message;
        Instance.gameObject.SetActive(true);
        Callback = callback;
    }
}
