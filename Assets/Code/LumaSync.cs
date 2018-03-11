using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes sure the total luma of the sliders under the advanced settings panel is 1.
/// </summary>
public class LumaSync : MonoBehaviour
{
    //references to other gameobjects/components
    public LumaSync OtherA, OtherB;
	public Slider slider;

	float Latest;

	void Start()
	{
		Latest = slider.value;
	}

    /// <summary>
    /// Updates the values of the two other sliders.
    /// </summary>
	public void Sync()
	{
        //skip heavy calculations if no change was made
		if (slider.value != Latest)
			Latest = slider.value;
		else return;

        //avoid division by zero and split it evenly instead
		if (OtherA.Get() == 0 && OtherB.Get() == 0)
		{
			OtherA.Set((1 - Latest) / 2f);
			OtherB.Set((1 - Latest) / 2f);
		}
		else
		{
            //scale the new luma of each column proportionally the current values
			float multiplier = (1 - Latest) / (OtherA.Get() + OtherB.Get());
			OtherA.Set(OtherA.Get() * multiplier);
			OtherB.Set(OtherB.Get() * multiplier);
		}
	}

    /// <summary>
    /// Get the value of this slider.
    /// </summary>
    /// <returns>The current slider value.</returns>
	public float Get()
	{
		return Latest;
	}

    /// <summary>
    /// Set the value of this slider.
    /// </summary>
    /// <param name="value">The new value.</param>
	public void Set(float value)
	{
		slider.value = Latest = value;
	}
}
