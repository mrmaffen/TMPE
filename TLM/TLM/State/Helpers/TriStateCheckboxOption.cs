namespace TrafficManager.State.Helpers {
    using ICities;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using UnityEngine;
    using TrafficManager.Util.Extensions;
    using System;
    using TrafficManager.UI.Helpers;

    public class TriStateCheckboxOption : UIOptionBase<bool?> {
        private const int LABEL_MAX_WIDTH = 615;
        private const int LABEL_MAX_WIDTH_INDENTED = 600;
        protected UITriStateCheckbox ui_;

        public virtual TriStateCheckboxOption AddUI(UIHelperBase container) {
            ui_ = container.AddUIComponent<UITriStateCheckbox>();
            ui_.EventValueChanged += OnValueChanged;
            if (Indent) ApplyIndent(ui_);
            InitUI(ui_);
            ApplyTextWrap(ui_, Indent);
            return this;
        }

        public override void SetValue(bool? value) => ui_.Value = value;

        protected override void UpdateLabel() {
            if (ui_ != null) {
                ui_.label.text = Translate(Label);
            }
        }

        protected override void UpdateTooltip() {
            if (ui_ == null) return;

            if (!IsInScope) {
                ui_.tooltip = Translate(INGAME_ONLY_SETTING);
            } else if (string.IsNullOrEmpty(_tooltip)) {
                ui_.tooltip = string.Empty;
            } else {
                ui_.tooltip = Translate(_tooltip);
            }
        }

        protected override void UpdateReadOnly() {
            if (ui_ == null) return;

            var readOnly = !IsInScope || _readOnly;

            Log._Debug($"TriStateCheckboxOption.UpdateReadOnly() - `Name` is {(readOnly ? "read-only" : "writeable")}");

            ui_.readOnly = readOnly;
            ui_.opacity = readOnly ? 0.3f : 1f;
        }

        /* UI helper methods */

        internal static void ApplyIndent(UIComponent component) {
            UILabel label = component.Find<UILabel>("Label");

            if (label != null) {
                label.padding = new RectOffset(22, 0, 0, 0);
            }

            UISprite check = component.Find<UISprite>("Unchecked");

            if (check != null) {
                check.relativePosition += new Vector3(22.0f, 0);
            }
        }

        internal static void ApplyTextWrap(UICheckBox checkBox, bool indented = false) {
            UILabel label = checkBox.label;
            bool requireTextWrap;
            int maxWidth = indented ? LABEL_MAX_WIDTH_INDENTED : LABEL_MAX_WIDTH;
            using (UIFontRenderer renderer = label.ObtainRenderer()) {
                Vector2 size = renderer.MeasureString(label.text);
                requireTextWrap = size.x > maxWidth;
            }
            label.autoSize = false;
            label.wordWrap = true;
            label.verticalAlignment = UIVerticalAlignment.Middle;
            label.textAlignment = UIHorizontalAlignment.Left;
            label.size = new Vector2(maxWidth, requireTextWrap ? 40 : 20);
            if (requireTextWrap) {
                checkBox.height = 42; // set new height + top/bottom 1px padding
            }
        }
    }
}