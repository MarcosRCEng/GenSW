using System.Runtime.InteropServices;

namespace GenSW.OptimizationWorker;

// Signatures correspond to the 32-bit HighsInt C ABI in v1.15.1, checked at startup.
internal static class HighsNative
{
    private const string Library = "highs";

    [DllImport(Library, EntryPoint = "Highs_version", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint Version();

    [DllImport(Library, EntryPoint = "Highs_create", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint Create();

    [DllImport(Library, EntryPoint = "Highs_destroy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Destroy(nint solver);

    [DllImport(Library, EntryPoint = "Highs_getSizeofHighsInt", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetSizeofHighsInt(nint solver);

    [DllImport(Library, EntryPoint = "Highs_setBoolOptionValue", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SetBoolOption(nint solver, string option, int value);

    [DllImport(Library, EntryPoint = "Highs_setStringOptionValue", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SetStringOption(nint solver, string option, string value);

    [DllImport(Library, EntryPoint = "Highs_setIntOptionValue", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SetIntOption(nint solver, string option, int value);

    [DllImport(Library, EntryPoint = "Highs_setDoubleOptionValue", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SetDoubleOption(nint solver, string option, double value);

    [DllImport(Library, EntryPoint = "Highs_getStringOptionValue", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetStringOption(nint solver, string option, [Out] byte[] value);

    [DllImport(Library, EntryPoint = "Highs_getIntOptionValue", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetIntOption(nint solver, string option, out int value);

    [DllImport(Library, EntryPoint = "Highs_getDoubleOptionValue", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetDoubleOption(nint solver, string option, out double value);

    [DllImport(Library, EntryPoint = "Highs_getInfinity", CallingConvention = CallingConvention.Cdecl)]
    internal static extern double GetInfinity(nint solver);

    [DllImport(Library, EntryPoint = "Highs_passLp", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int PassLp(nint solver, int columns, int rows, int nonZero, int format, int sense, double offset,
        double[] costs, double[] columnLower, double[] columnUpper, double[] rowLower, double[] rowUpper,
        int[] starts, int[] indices, double[] values);

    [DllImport(Library, EntryPoint = "Highs_run", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int Run(nint solver);

    [DllImport(Library, EntryPoint = "Highs_getModelStatus", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetModelStatus(nint solver);

    [DllImport(Library, EntryPoint = "Highs_getIntInfoValue", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetIntInfo(nint solver, string info, out int value);

    [DllImport(Library, EntryPoint = "Highs_getSolution", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetSolution(nint solver, [Out] double[] columns, [Out] double[] columnDuals,
        [Out] double[] rows, [Out] double[] rowDuals);

    [DllImport(Library, EntryPoint = "Highs_getObjectiveValue", CallingConvention = CallingConvention.Cdecl)]
    internal static extern double GetObjectiveValue(nint solver);

    [DllImport(Library, EntryPoint = "Highs_getDualRay", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetDualRay(nint solver, out int hasRay, [Out] double[] values);
}
