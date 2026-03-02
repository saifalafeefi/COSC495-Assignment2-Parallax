using UnityEngine;

// drop on any button — plays the UI select sound on click
// works in any scene since it finds SFXManager at runtime
public class UISelectSFX : MonoBehaviour
{
    public void Play()
    {
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayUISelect();
    }
}
