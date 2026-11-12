using UnityEngine;

namespace Evo.UI.Demo
{
    /// <summary>
    /// Opens a web page. Hook Open to a button's On Click.
    /// </summary>
    [AddComponentMenu("")]
    public class DemoLink : MonoBehaviour
    {
        [SerializeField] private string url;

        public void Open()
        {
            if (!string.IsNullOrEmpty(url))
                Application.OpenURL(url);
        }
    }
}
