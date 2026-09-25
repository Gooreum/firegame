namespace FireGame.Prototypes
{
    /// <summary>시험판 한 판. PrototypeHost가 매 프레임 입력을 넘기고, 다시 시작·전환 때 지운다.</summary>
    public interface IPrototype
    {
        void Tick(float dt, in ProtoInput input);
        void Destroy();
    }
}
