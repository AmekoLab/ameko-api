using System;

namespace FPTU.Capstone.AMKCollective.Application.Helpers;

public class CursorPagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public string? NextCursor { get; set; }
    public bool HasMore { get; set; }
}

public static class CursorHelper
{
    public static string EncodeCursor(DateTime createdAt, int id)
    {
        return $"{createdAt:O}_{id}";
    }

    public static string EncodeCursor(DateTime createdAt, Guid id)
    {
        return $"{createdAt:O}_{id}";
    }

    public static (DateTime CreatedAt, int Id)? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;

        var parts = cursor.Split('_');
        if (parts.Length == 2 && int.TryParse(parts[1], out var id))
        {
            try 
            {
                var date = DateTime.Parse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind);
                return (date.ToUniversalTime(), id); 
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public static (DateTime CreatedAt, Guid Id)? DecodeGuidCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;

        var parts = cursor.Split('_');
        if (parts.Length == 2 && Guid.TryParse(parts[1], out var id))
        {
            try 
            {
                var date = DateTime.Parse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind);
                return (date.ToUniversalTime(), id); 
            }
            catch
            {
                return null;
            }
        }
        return null;
    }
}
