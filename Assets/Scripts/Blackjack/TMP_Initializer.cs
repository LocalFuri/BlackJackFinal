using TMPro;
using UnityEngine;

namespace Blackjack
{
    /// <summary>
    /// Forces TextMeshPro's default font asset to initialize its internal material reference
    /// before the Canvas flushes its first rebuild queue on frame 0.
    ///
    /// Without this, TMP_FontAsset.material can be null during the very first
    /// OnPreRenderCanvas pass, causing a NullReferenceException inside
    /// MaterialReference..ctor at MaterialReferenceManager.cs:525.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class TMP_Initializer : MonoBehaviour
    {
        private void Awake()
        {
            // Accessing the default font asset forces TMP_Settings to load and
            // initialize the font, which populates its internal material reference
            // before any TextMeshProUGUI component attempts a Canvas rebuild.
            _ = TMP_Settings.defaultFontAsset;
        }
    }
}
