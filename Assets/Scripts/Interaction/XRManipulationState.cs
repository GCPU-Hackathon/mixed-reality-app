// XRManipulationState.cs
public static class XRManipulationState
{
    public static bool TranslatingActive = false;
    public static bool ScalingActive     = false;
    public static bool RotatingActive    = false;

    public static bool SomeoneActive =>
        TranslatingActive || ScalingActive || RotatingActive;

    public static void ResetAll()
    {
        TranslatingActive = false;
        ScalingActive     = false;
        RotatingActive    = false;
    }
}
