using Modding;

public interface ITCMod
{
    void OnModLoaded(ModManifest manifest);
    void OnModUnLoaded();
    void OnFrame();
}
