using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Information popup, notifying the user and having them click a button to continue.
/// </summary>
public class Popup : MonoBehaviour
{
    //references to other gameobjects/components
    public Text text;

    /// <summary>
    /// Closes the popup and calls the callback should there be one.
    /// </summary>
    public void Close()
    {
        gameObject.SetActive(false);
        text.text = string.Empty;
        if (Callback != null)
            Callback();
    }

    public static Popup Instance;                                       //static reference to the (only) popup instance
    public static System.Action Callback = null;
    public static KeyValuePair<string, System.Action>? queued = null;   //set in a worker thread to trigger a popup the next main update

    /// <summary>
    /// Opens the popup.
    /// </summary>
    /// <param name="message">The string to be displayed by the popup.</param>
    /// <param name="callback">Some optional code to be run when the user closes the popup.</param>
    public static void ActivatePopup(string message, System.Action callback = null)
    {
        Instance.text.text = message;
        Instance.gameObject.SetActive(true);
        Callback = callback;
    }
}
