namespace CloningService.Tests.types
{
    public class Simple
    {
        public int I;
        public string S { get; set; }
        
        [Cloneable(CloningMode.Ignore)] 
        public string Ignored { get; set; }
        
        [Cloneable(CloningMode.Shallow)] 
        public object Shallow { get; set; }

        public virtual string Computed => S + I + Shallow;
    }
}