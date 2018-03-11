using UnityEngine;
using System.IO;

/// <summary>
/// The class handling most of the transitions between screens due to user interaction,
/// such as the help and advanced settings buttons.
/// </summary>
public class ExtraButtons : MonoBehaviour
{
    //references to other gameobjects/components
    public GameObject HelpPanel, PicturePanel, AdvancedSettingsPanel;
	public SettingExtender SettingManager;

    //prompt messages
    public const string DeleteBitmapsPrompt = "Delete saved bitmaps as well?";

    //most of these methods are fairly self-descriptive
    //they basically deactivate one gameobject and activate another

    public void DisplayHelp()
	{
		HelpPanel.SetActive(true);
		PicturePanel.SetActive(false);
        TakePicture.Instance.taken = true;
        TakePicture.Instance.CameraUI.transform.parent.gameObject.SetActive(false);
    }

	public void ReturnFromHelp()
	{
		HelpPanel.SetActive(false);
		PicturePanel.SetActive(true);
        TakePicture.Instance.taken = false;
        TakePicture.Instance.CameraUI.transform.parent.gameObject.SetActive(true);
    }

    public void DirectlyToAdvanced()
    {
        AdvancedSettingsPanel.SetActive(true);
        PicturePanel.SetActive(false);
        TakePicture.Instance.taken = true;
        TakePicture.Instance.CameraUI.transform.parent.gameObject.SetActive(false);
    }

    public void DirectlyFromAdvanced()
    {
        SettingManager.SaveSettings();
        AdvancedSettingsPanel.SetActive(false);
        PicturePanel.SetActive(true);
        TakePicture.Instance.taken = false;
        TakePicture.Instance.CameraUI.transform.parent.gameObject.SetActive(true);
    }

    /// <summary>
    /// Resets all values under advanced settings to their defaults,
    /// and asks the user whether to remove all stored bitmaps.
    /// </summary>
	public void ResetSettings()
	{
		SettingExtender.SaveDefaultSettings();
		SettingManager.LoadSettings();
        QuestionPopup.ActivatePopup(DeleteBitmapsPrompt, yes =>
        {
            if (yes)
            {
                string path = Path.Combine(BitmapEncoding.PersistentPath, BitmapEncoding.BitmapFileName);
                if (File.Exists(path))
                    File.Delete(path);
                print("Deleted bitmaps");
                TakePicture.Instance.storedBitmaps = BitmapEncoding.LoadBitmaps();
            }
        });
	}
}
