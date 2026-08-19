using UnityEditor;

public static class FolderTreeSettings
{
    private const string PrefKey = "LenzDev_ShowFolderLines";

    public static bool ShowLines
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }
    
    public static bool SingleColumnColoring
    {
        get => EditorPrefs.GetBool("LenzDev_SingleColumnColoring", false);
        set => EditorPrefs.SetBool("LenzDev_SingleColumnColoring", value);
    }
}