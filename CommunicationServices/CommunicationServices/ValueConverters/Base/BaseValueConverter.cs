using System;
using System.Globalization;
using Windows.UI.Xaml.Data;

namespace CallingQuickstart
{
	public abstract class BaseValueConverter : IValueConverter
	{


		#region Value Converter Methods

        public abstract object Convert(object value, Type targetType, object parameter, string language);

		public abstract object ConvertBack(object value, Type targetType, object parameter, string language);

        #endregion
    }
}
