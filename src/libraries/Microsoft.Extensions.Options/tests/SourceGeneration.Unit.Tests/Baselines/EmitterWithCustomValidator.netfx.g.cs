
    // <auto-generated/>
    #nullable enable
    #pragma warning disable CS1591 // Compensate for https://github.com/dotnet/roslyn/issues/54103
    namespace HelloWorld
{
    partial struct MyOptionsValidator
    {
        /// <summary>
        /// Validates a specific named options instance (or all when <paramref name="name"/> is <see langword="null" />).
        /// </summary>
        /// <param name="name">The name of the options instance being validated.</param>
        /// <param name="options">The options instance.</param>
        /// <returns>Validation result.</returns>
        [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Extensions.Options.SourceGeneration", "42.42.42.42")]
        public global::Microsoft.Extensions.Options.ValidateOptionsResult Validate(string? name, global::HelloWorld.MyOptions options)
        {
            global::Microsoft.Extensions.Options.ValidateOptionsResultBuilder? builder = null;
            #if NET10_0_OR_GREATER
            string displayName = string.IsNullOrEmpty(name) ? "MyOptions.Validate" : $"{name}.Validate";
            var context = new global::System.ComponentModel.DataAnnotations.ValidationContext(options, displayName, null, null);
            #else
            var context = new global::System.ComponentModel.DataAnnotations.ValidationContext(options);
            #endif
            var validationResults = new global::System.Collections.Generic.List<global::System.ComponentModel.DataAnnotations.ValidationResult>();
            var validationAttributes = new global::System.Collections.Generic.List<global::System.ComponentModel.DataAnnotations.ValidationAttribute>(1);

            context.MemberName = "Val1";
            context.DisplayName = string.IsNullOrEmpty(name) ? "MyOptions.Val1" : $"{name}.Val1";
            validationAttributes.Add(global::__OptionValidationStaticInstances.__Attributes.A1);
            if (!global::System.ComponentModel.DataAnnotations.Validator.TryValidateValue(options.Val1, context, validationResults, validationAttributes))
            {
                (builder ??= new()).AddResults(validationResults);
            }

            context.MemberName = "Val2";
            context.DisplayName = string.IsNullOrEmpty(name) ? "MyOptions.Val2" : $"{name}.Val2";
            validationResults.Clear();
            validationAttributes.Clear();
            validationAttributes.Add(global::__OptionValidationStaticInstances.__Attributes.A2);
            if (!global::System.ComponentModel.DataAnnotations.Validator.TryValidateValue(options.Val2, context, validationResults, validationAttributes))
            {
                (builder ??= new()).AddResults(validationResults);
            }

            return builder is null ? global::Microsoft.Extensions.Options.ValidateOptionsResult.Success : builder.Build();
        }
    }
}
namespace __OptionValidationStaticInstances
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Extensions.Options.SourceGeneration", "42.42.42.42")]
    file static class __Attributes
    {
        internal static readonly global::System.ComponentModel.DataAnnotations.RequiredAttribute A1 = new global::System.ComponentModel.DataAnnotations.RequiredAttribute();

        internal static readonly __OptionValidationGeneratedAttributes.__SourceGen__RangeAttribute A2 = new __OptionValidationGeneratedAttributes.__SourceGen__RangeAttribute(
            (int)1,
            (int)3);
    }
}
namespace __OptionValidationStaticInstances
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Extensions.Options.SourceGeneration", "42.42.42.42")]
    file static class __Validators
    {
    }
}
namespace __OptionValidationGeneratedAttributes
{
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Extensions.Options.SourceGeneration", "42.42.42.42")]
    [global::System.AttributeUsage(global::System.AttributeTargets.Property | global::System.AttributeTargets.Field | global::System.AttributeTargets.Parameter, AllowMultiple = false)]
    file class __SourceGen__RangeAttribute : global::System.ComponentModel.DataAnnotations.ValidationAttribute
    {
        public __SourceGen__RangeAttribute(int minimum, int maximum) : base()
        {
            Minimum = minimum;
            Maximum = maximum;
            OperandType = typeof(int);
        }
        public __SourceGen__RangeAttribute(double minimum, double maximum) : base()
        {
            Minimum = minimum;
            Maximum = maximum;
            OperandType = typeof(double);
        }
        public __SourceGen__RangeAttribute(global::System.Type type, string minimum, string maximum) : base()
        {
            OperandType = type;
            _needToConvertMinMax = true;
            Minimum = minimum;
            Maximum = maximum;
        }
        public object Minimum { get; private set; }
        public object Maximum { get; private set; }
        public bool MinimumIsExclusive { get; set; }
        public bool MaximumIsExclusive { get; set; }
        public global::System.Type OperandType { get; }
        public bool ParseLimitsInInvariantCulture { get; set; }
        public bool ConvertValueInInvariantCulture { get; set; }
        public override string FormatErrorMessage(string name) =>
                string.Format(global::System.Globalization.CultureInfo.CurrentCulture, GetValidationErrorMessage(), name, Minimum, Maximum);
        private readonly bool _needToConvertMinMax;
        private volatile bool _initialized;
        private readonly object _lock = new();
        private const string MinMaxError = "The minimum and maximum values must be set to valid values.";

        public override bool IsValid(object? value)
        {
            if (!_initialized)
            {
                lock (_lock)
                {
                    if (!_initialized)
                    {
                        if (Minimum is null || Maximum is null)
                        {
                            throw new global::System.InvalidOperationException(MinMaxError);
                        }
                        if (_needToConvertMinMax)
                        {
                            System.Globalization.CultureInfo culture = ParseLimitsInInvariantCulture ? global::System.Globalization.CultureInfo.InvariantCulture : global::System.Globalization.CultureInfo.CurrentCulture;
                            Minimum = ConvertValue(Minimum, culture) ?? throw new global::System.InvalidOperationException(MinMaxError);
                            Maximum = ConvertValue(Maximum, culture) ?? throw new global::System.InvalidOperationException(MinMaxError);
                        }
                        int cmp = ((global::System.IComparable)Minimum).CompareTo((global::System.IComparable)Maximum);
                        if (cmp > 0)
                        {
                            throw new global::System.InvalidOperationException("The maximum value '{Maximum}' must be greater than or equal to the minimum value '{Minimum}'.");
                        }
                        else if (cmp == 0 && (MinimumIsExclusive || MaximumIsExclusive))
                        {
                            throw new global::System.InvalidOperationException("Cannot use exclusive bounds when the maximum value is equal to the minimum value.");
                        }
                        _initialized = true;
                    }
                }
            }

            if (value is null or string { Length: 0 })
            {
                return true;
            }

            System.Globalization.CultureInfo formatProvider = ConvertValueInInvariantCulture ? global::System.Globalization.CultureInfo.InvariantCulture : global::System.Globalization.CultureInfo.CurrentCulture;
            object? convertedValue;

            try
            {
                convertedValue = ConvertValue(value, formatProvider);
            }
            catch (global::System.Exception e) when (e is global::System.FormatException or global::System.InvalidCastException or global::System.NotSupportedException)
            {
                return false;
            }

            var min = (global::System.IComparable)Minimum;
            var max = (global::System.IComparable)Maximum;

            return
                (MinimumIsExclusive ? min.CompareTo(convertedValue) < 0 : min.CompareTo(convertedValue) <= 0) &&
                (MaximumIsExclusive ? max.CompareTo(convertedValue) > 0 : max.CompareTo(convertedValue) >= 0);
        }
        private string GetValidationErrorMessage()
        {
            return (MinimumIsExclusive, MaximumIsExclusive) switch
            {
                (false, false) => "The field {0} must be between {1} and {2}.",
                (true, false) => "The field {0} must be between {1} exclusive and {2}.",
                (false, true) => "The field {0} must be between {1} and {2} exclusive.",
                (true, true) => "The field {0} must be between {1} exclusive and {2} exclusive.",
            };
        }
        private object? ConvertValue(object? value, System.Globalization.CultureInfo formatProvider)
        {
            if (value is string stringValue)
            {
                value = global::System.Convert.ChangeType(stringValue, OperandType, formatProvider);
            }
            else
            {
                value = global::System.Convert.ChangeType(value, OperandType, formatProvider);
            }
            return value;
        }
    }
}
