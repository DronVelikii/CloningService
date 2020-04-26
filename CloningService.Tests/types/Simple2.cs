namespace CloningService.Tests.types
{
    public class Simple2 : Simple
    {
        public double D;
        public SimpleStruct SS;
        public override string Computed => S + I + D + SS.Computed;
    }
}