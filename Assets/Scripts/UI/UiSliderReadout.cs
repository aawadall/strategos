// UiSliderReadout.cs
// Keeps a slider's numeric label in step with its value, including when the value is set
// programmatically.
//
// WHY THIS EXISTS
// The obvious place for the label update is the onValueChanged listener, and that is where it
// started. But SetValueWithoutNotify deliberately does not fire that listener — which is
// exactly what you want when seeding controls, since notifying would trip every view's
// re-entrancy guard — so the value moved and the label did not. The scenario view's sixteen
// relief sliders all showed their minimums while their handles sat at the profile's real
// values: the handles were right and the numbers were lying.
//
// Attaching the formatter to the slider means the label cannot be forgotten. Set values
// through UiFactory.SetSliderValue and both stay correct.

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Strategos.UI
{
    [RequireComponent(typeof(Slider))]
    public sealed class UiSliderReadout : MonoBehaviour
    {
        private Slider _slider;
        private TMP_Text _label;
        private string _format = "0";
        private string _suffix = string.Empty;

        public void Init(Slider slider, TMP_Text label, string format, string suffix)
        {
            _slider = slider;
            _label = label;
            _format = format;
            _suffix = suffix;
            Refresh();
        }

        /// <summary>Rewrites the label from the slider's current value.</summary>
        public void Refresh()
        {
            if (_label == null || _slider == null) return;
            _label.text = _slider.value.ToString(_format) + _suffix;
        }

        /// <summary>
        /// Sets the value without firing onValueChanged, and updates the label. This is the
        /// pairing that is easy to get half-right by hand.
        /// </summary>
        public void SetValueSilently(float value)
        {
            if (_slider == null) return;
            _slider.SetValueWithoutNotify(value);
            Refresh();
        }
    }
}
