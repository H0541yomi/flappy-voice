namespace FlappyVoice.Gameplay
{
    public interface IHeightSource
    {
        float TargetHeight01 { get; }
        bool IsActive { get; }
    }
}
