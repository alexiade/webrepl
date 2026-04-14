namespace WebREPL.Core;

public static class PythonSnippets
{
    public const string Marker = "THE_END_OF_THIS_GENERATED_COMMAND";

    public static string ListDirectory()
    {
        return "import os;print(';'.join([f'{f},{os.stat(f)[0] & 0x4000 != 0},{os.stat(f)[6]}' for f in os.listdir()]))";
    }

    public static string ChangeDirectory(string pathEscaped)
    {
        return $"import os; os.chdir('{pathEscaped}')";
    }

    public static string GetCurrentDirectory()
    {
        return "import os;print(os.getcwd())";
    }

    public static string DeleteFile(string pathEscaped)
    {
        return $"import os; os.remove('{pathEscaped}')";
    }

    public static string MakeDirectory(string pathEscaped)
    {
        return $"import os; os.mkdir('{pathEscaped}')";
    }

    public static string RemoveDirectory(string pathEscaped)
    {
        return $"import os; os.rmdir('{pathEscaped}')";
    }

    public static string SoftReset()
    {
        return "import machine; machine.soft_reset()";
    }

    public static string HardReset()
    {
        return "import machine; machine.reset()";
    }

    public static string WrapWithMarker(string pythonExpression)
    {
        return $"{pythonExpression};print('{Marker}')";
    }
}
