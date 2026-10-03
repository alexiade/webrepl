namespace WebREPL.Core;

public static class PythonSnippets
{
    public const string Marker = "THE_END_OF_THIS_GENERATED_COMMAND";

    /// <summary>
    /// name,isDir,size entries separated by ';'. Uses os.ilistdir so the path is honoured and
    /// sizes come in the same call; directories report 0 (LittleFS has no meaningful size for
    /// them, and stat() returns whatever was left over from the previous call).
    /// </summary>
    public static string ListDirectory(string pathEscaped = "")
    {
        return "import os;print(';'.join(['{},{},{}'.format(e[0], e[1] == 0x4000, e[3] if len(e) > 3 and e[1] != 0x4000 else 0)"
            + $" for e in os.ilistdir('{pathEscaped}')]))";
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
