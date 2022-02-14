using System;
using System.Globalization;

namespace CallingQuickstart
{
    public class NullToBooleanConverter : BaseValueConverter
    {
        public override object Convert(object value, Type targetType, object parameter, string language) =>
            !string.IsNullOrEmpty((string)value);

        public override object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
