using Asuna.Dialogues;
using Modding;

public interface ITCMod
{
    void OnModLoaded(ModManifest manifest);
    void OnModUnLoaded();
    void OnDialogueStarted(Dialogue dialogue);
    void OnLineStarted(DialogueLine line);
    void OnFrame();
}
