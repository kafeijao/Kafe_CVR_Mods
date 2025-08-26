using System.Text;
using System.Runtime.InteropServices;

namespace Kafe.OSC.Utils;

/// <summary>
/// A fixed-capacity, inline UTF-8 string type with no heap allocations.
/// Capacity: 4096 bytes.
/// </summary>
/// <remarks>
/// Stores text as raw UTF-8 bytes in a fixed-size buffer inside the struct.
/// Fully unmanaged, suitable for use in <c>where T : unmanaged</c> scenarios like SPSC queues.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct FixedUtf8String4096
{
    private const int Capacity = 4096;
    private fixed byte buffer[Capacity];

    /// <summary>Current number of bytes used.</summary>
    public int Length { get; private set; }

    /// <summary>Maximum number of bytes available.</summary>
    public int MaxCapacity => Capacity;

    /// <summary>Sets the string to the given UTF-8 text, truncating if needed.</summary>
    /// <param name="value">Managed string to encode.</param>
    public void Set(string value)
    {
        if (value == null)
        {
            Length = 0;
            return;
        }

        fixed (byte* ptr = buffer)
        fixed (char* cptr = value)
        {
            // Encode directly into the fixed byte buffer
            int written = Encoding.UTF8.GetBytes(cptr, value.Length, ptr, Capacity);
            Length = written;
        }
    }


    /// <summary>Returns the string as a managed <see cref="string"/>.</summary>
    public override string ToString()
    {
        if (Length <= 0) return string.Empty;
        fixed (byte* ptr = buffer)
        {
            return Encoding.UTF8.GetString(ptr, Length);
        }
    }

    /// <summary>Copies the raw UTF-8 bytes into a destination span.</summary>
    /// <param name="destination">Destination span. Must be at least <see cref="Length"/> bytes.</param>
    /// <exception cref="ArgumentException">Thrown if destination is too small.</exception>
    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < Length)
            throw new ArgumentException("Destination span too small", nameof(destination));

        fixed (byte* ptr = buffer)
        {
            new ReadOnlySpan<byte>(ptr, Length).CopyTo(destination);
        }
    }

    /// <summary>Clears the string without allocating.</summary>
    public void Clear() => Length = 0;
}
