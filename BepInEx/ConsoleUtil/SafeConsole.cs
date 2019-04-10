// --------------------------------------------------
// UnityInjector - SafeConsole.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.Reflection;

namespace BepInEx.ConsoleUtil
{
	internal static class SafeConsole
	{
		private static GetColorDelegate _getBackgroundColor;
		private static GetColorDelegate _getForegroundColor;
		private static SetColorDelegate _setBackgroundColor;
		private static SetColorDelegate _setForegroundColor;
		private static ResetColorDelegate _resetColor;

		public static ConsoleColor BackgroundColor
		{
			get => _getBackgroundColor();
			set => _setBackgroundColor(value);
		}

		public static ConsoleColor ForegroundColor
		{
			get => _getForegroundColor();
			set => _setForegroundColor(value);
		}

		public static void ResetColor() => _resetColor(); 

        static SafeConsole()
		{
			var tConsole = typeof(Console);
			InitColors(tConsole);
		}

		private static void SetColorFunction<T>(ref T del, MethodInfo cf, T kon, T none) where T : class
		{
			if (cf != null)
				del = Delegate.CreateDelegate(typeof(T), cf) as T;
			else if (Utility.IsOnWindows)
				del = kon;
			else
				del = none;
        }

		private static void InitColors(Type tConsole)
		{
			const BindingFlags BINDING_FLAGS = BindingFlags.Public | BindingFlags.Static;

			var sfc = tConsole.GetMethod("set_ForegroundColor", BINDING_FLAGS);
			var sbc = tConsole.GetMethod("set_BackgroundColor", BINDING_FLAGS);
			var gfc = tConsole.GetMethod("get_ForegroundColor", BINDING_FLAGS);
			var gbc = tConsole.GetMethod("get_BackgroundColor", BINDING_FLAGS);
			var rc = tConsole.GetMethod("ResetColor", BINDING_FLAGS);

			SetColorFunction(ref _setForegroundColor, sfc, c => Kon.ForegroundColor = c, _ => { });
			SetColorFunction(ref _setBackgroundColor, sbc, c => Kon.BackgroundColor = c, _ => { });
			SetColorFunction(ref _getForegroundColor, gfc, () => Kon.ForegroundColor, () => ConsoleColor.Gray);
			SetColorFunction(ref _getBackgroundColor, gbc, () => Kon.BackgroundColor, () => ConsoleColor.Black);

			SetColorFunction(ref _resetColor, rc, Kon.ResetConsoleColor, () => {});
		}

		private delegate ConsoleColor GetColorDelegate();

		private delegate void ResetColorDelegate();

		private delegate void SetColorDelegate(ConsoleColor value);
	}
}