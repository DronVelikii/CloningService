namespace CloningService.Tests.types
{
    public class Node
    {
        public Node Left;
        public Node Right;
        public object Value;

        public int TotalNodeCount => 1 + (Left?.TotalNodeCount ?? 0) + (Right?.TotalNodeCount ?? 0);
    }
}