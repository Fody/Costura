using System.Collections.Generic;
using System.Linq;

public static class Extensions
{
    public static IEnumerable<string> NonEmpty(this IEnumerable<string> list)
    {
        return list.Select(x => x.Trim()).Where(x => x != string.Empty);
    } 
}