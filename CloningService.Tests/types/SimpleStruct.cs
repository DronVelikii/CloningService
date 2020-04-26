namespace CloningService.Tests.types
{
    public struct SimpleStruct
    {
        public int I;
        public string S { get; set; }

        [Cloneable(CloningMode.Ignore)] 
        public string Ignored { get; set; }

        public string Computed => S + I;

        public SimpleStruct(int i, string s)
        {
            I = i;
            S = s;
            Ignored = null;
        }
    }
}