using Commons.Borrowing;

namespace Commons
{
    public interface IBorrowable
    {
        string Title { get; }
        Category Category { get; }
    }
}