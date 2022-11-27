namespace TrafficManager.State.Helpers {
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons;
    using ICities;
    using System;
    using TrafficManager.State;
    using TrafficManager.Lifecycle;
    using System.Runtime.CompilerServices;
    using System.Xml.Serialization;
    using System.Xml.Schema;
    using System.Xml;

    public abstract class SerializableOptionBase : ILegacySerializableOption, IXmlSerializable {
        /// <summary>Use as tooltip for readonly UI components.</summary>
        public delegate string TranslatorDelegate(string key);

        protected const string INGAME_ONLY_SETTING = "This setting can only be changed in-game.";

        public string Name { get; private set; }

        public Scope Scope { get; private set; }

        public SerializableOptionBase(string name, Scope scope) {
            Name = name;
            Scope = scope;
        }

        public abstract void ResetValue();

        public abstract void Load(byte data);
        public abstract byte Save();
        public XmlSchema GetSchema() => null;
        public abstract void ReadXml(XmlReader reader);
        public abstract void WriteXml(XmlWriter writer);
    }

    public abstract class SerializableOptionBase<TVal> : SerializableOptionBase {
        public delegate TVal ValidatorDelegate(TVal desired, out TVal result);

        public delegate void OnChanged(TVal value);

        // used as internal store of value if _fieldInfo is null
        private TVal _value = default;

        protected TVal _defaultValue = default;

        public event OnChanged OnValueChanged;

        public SerializableOptionBase(string name, Scope scope)
            : base(name, scope) {
            OnValueChanged = DefaultOnValueChanged;
        }

        public OnChanged Handler {
            set {
                OnValueChanged -= value;
                OnValueChanged += value;
            }
        }

        /// <summary>
        /// Optional custom validator which intercepts value changes and can inhibit event propagation.
        /// </summary>
        public ValidatorDelegate Validator { get; set; }

        public virtual TVal Value {
            get => _value;
            set {
                if (!_value.Equals(value)) {
                    _value = value;
                    OnValueChanged(value);
                }
            }
        }

        /// <summary>set only during initialization</summary>
        public TVal DefaultValue {
            get => _defaultValue;
            set => _value = _defaultValue = value;
        }

        public TVal FastValue {
            [MethodImpl(256)]
            get => _value;
        }

        [MethodImpl(256)]

        public static implicit operator TVal(SerializableOptionBase<TVal> a) => a._value;

        public void DefaultOnValueChanged(TVal newVal) {
            if (Value.Equals(newVal)) {
                return;
            }
            Log._Debug($"SerializableOptionBase.DefaultOnValueChanged: {Name} value changed to {newVal}");
            Value = newVal;
        }

        public override void ResetValue() => Value = DefaultValue;

        public void InvokeOnValueChanged(TVal value) => OnValueChanged?.Invoke(value);
        public override void WriteXml(XmlWriter writer) => writer.WriteString(Value.ToString());

    }
}