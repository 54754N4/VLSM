using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Numerics;

namespace Subnetter
{
    #region IPv4 subnetting

    public readonly struct IP4
    {
        public static readonly int MAX_BITS = 32,
            MAX_OCTET = 0xFF, MIN_OCTET = 0,
            OCTET_BITS = 8, OCTETS = 4;
        public static readonly uint OCTET_MASK = 0xFF;
        public static readonly char SEPARATOR = '.';

        private readonly uint bits;

        public IP4(uint bits) 
            => this.bits = bits;

        public static IP4 operator &(IP4 a, IP4 b)
            => new(bits: a.bits & b.bits);

        public static IP4 operator |(IP4 a, IP4 b)
            => new(bits: a.bits | b.bits);

        public static IP4 operator ~(IP4 a)
            => new(bits: ~a.bits);

        public static IP4 operator +(IP4 a, uint offset)
            => new(bits: a.bits + offset);

        public static IP6 operator +(uint offset, IP4 a)
            => new(bits: a.bits + offset);

        public static IP4 operator -(IP4 a, uint offset)
            => new(bits: a.bits - offset);

        public static uint operator -(IP4 a, IP4 b)
            => a.bits - b.bits;

        public IEnumerable<byte> Octets() 
        {
            for (int i = OCTETS - 1; i >= 0; i--)
                yield return (byte)((bits & (OCTET_MASK << (OCTET_BITS * i))) >> (OCTET_BITS * i));
        }

        public IEnumerable<ValueTuple<int, byte>> OctetsIndexed()
        {
            var i = 0;
            foreach (var octet in Octets())
                yield return (i++, octet);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (byte octet in Octets())
                sb.Append(octet).Append(SEPARATOR);
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        public static IP4 From(string str)
        {
            var split = str.Split(SEPARATOR);
            if (split.Length > 4 || split.Length == 0)
                throw new FormatException("Malformed IP address");

            uint bits = 0, octet;
            for (int i = OCTETS - 1; i >= 0; i--)
            {
                if (OCTETS - 1 - i < split.Length)
                {
                    if (!uint.TryParse(split[OCTETS - 1 - i], out octet))
                        throw new FormatException($"Octet #{i} is invalid : {split[OCTETS - 1 - i]}");
                }
                else
                    octet = 0;
                if (octet > MAX_OCTET || octet < MIN_OCTET)
                    throw new FormatException($"An octet can only be between [{MIN_OCTET}, {MAX_OCTET}]");
                bits |= octet << (OCTET_BITS * i);
            }
            return new(bits);
        }

        public static IP4 SubnetMask(int prefix)
        {
            int leftover = MAX_BITS - prefix;
            uint ones = (1u << prefix) - 1;
            return new IP4(ones << leftover);
        }

        public readonly struct Range
        {
            public readonly IP4 From, To;
            public readonly uint TotalAddresses;

            public Range(IP4 from, IP4 to)
            {
                From = from;
                To = to;
                TotalAddresses = to - from;
            }

            public override string ToString()
                => $"{From} -> {To}";
        }

        public readonly struct CIDR
        {
            public readonly int NetworkBits, HostBits, MaximumHosts, MaximumSubnets;
            public readonly IP4 SubnetMask, WildcardMask, NetworkAddress, BroadcastAddress;
            public readonly Range Range;

            public CIDR(IP4 ip, int prefix)
            {
                if (prefix < 0 || prefix > 32)
                    throw new ArgumentException("Invalid CIDR prefix");
                SubnetMask = IP4.SubnetMask(prefix);
                NetworkBits = prefix;
                HostBits = IP6.MAX_BITS - prefix;
                MaximumHosts = 1 << HostBits;
                MaximumSubnets = 1 << NetworkBits;
                WildcardMask = ~SubnetMask;
                NetworkAddress = ip & SubnetMask;
                BroadcastAddress = ip | WildcardMask;
                Range = new Range(NetworkAddress + 1, BroadcastAddress - 1);
            }

            public override string ToString()
                => $"Network bits: {NetworkBits}\n" +
                    $"Host bits: {HostBits}\n" +
                    $"Maximum hosts per subnet: {MaximumHosts} ({MaximumHosts - 2} valid hosts)\n" +  // -2 to exclude network address and broadcast address
                    $"Maximum subnets per network: {MaximumSubnets}\n" +
                    $"Subnetmask: {SubnetMask}\n" +
                    $"Wildcardmask: {WildcardMask}\n" +
                    $"Network address: {NetworkAddress}\n" +
                    $"Broadcast address: {BroadcastAddress}\n" +
                    $"Range: {Range}\n";

            public static CIDR From(string cidr)
            {
                if (!cidr.Contains('/'))
                    throw new ArgumentException("Prefix not found");
                var split = cidr.Split('/');
                return new(IP4.From(split[0]), int.Parse(split[1]));
            }
        }

        public readonly struct Subnet
        {
            public readonly string Name;
            public readonly uint Hosts;
            public readonly IP4 SubnetMask, NetworkAddress, BroadcastAddress;
            public readonly Range Range;

            public Subnet(string Name, uint Hosts, IP4 SubnetMask, IP4 NetworkAddress, IP4 BroadcastAddress, Range Range)
            {
                this.Name = Name;
                this.Hosts = Hosts;
                this.SubnetMask = SubnetMask;
                this.NetworkAddress = NetworkAddress;
                this.BroadcastAddress = BroadcastAddress;
                this.Range = Range;
            }

            public override string ToString()
                => $"Subnet name: {Name}\n" +
                    $"Hosts: {Hosts}\n" +
                    $"Subnetmask: {SubnetMask}\n" +
                    $"Network address: {NetworkAddress}\n" +
                    $"Broadcast address: {BroadcastAddress}\n" +
                    $"IP range: {Range}\n";
        }

        public readonly struct VLSM
        {
            public static IList<Subnet> For(IP4 start, Dictionary<string, uint> table)
            {
                var subnets = new List<Subnet>();
                var ip = start;
                foreach ((var name, var hosts) in table.OrderByDescending(x => x.Value))
                {
                    var hostBits = NearestPowerOf2(hosts + 2);  // account for the network + broadcast addresses
                    var networkBits = IP6.MAX_BITS - hostBits;
                    var sm = SubnetMask(networkBits);
                    var nw = ip & sm;
                    var bc = ip | ~sm;
                    var range = new Range(nw + 1, bc - 1);
                    subnets.Add(new Subnet(name, hosts, sm, nw, bc, range));
                    // For next subnet
                    ip = bc + 1;
                }
                return subnets;
            }

            public static int NearestPowerOf2(uint val)
            {
                var i = 0;
                while (1 << i < val)
                    i++;
                return i;
            }
        }
    }

    #endregion

    #region Unsinged int 128 bits

    public readonly struct UInt128 : IFormattable, IComparable<UInt128>, IEquatable<UInt128>
    {
        public static readonly int BYTES = 16,
            HALF_BITS = 64,
            MAX_BITS = 128;
        public static readonly UInt128 Zero = 0,
            One = 1,
            MinValue = Zero,
            MaxValue = new(ulong.MaxValue, ulong.MaxValue);

        private readonly ulong high, low;

        public UInt128(ulong high, ulong low)
        {
            this.high = high;
            this.low = low;
        }

        public UInt128(BigInteger value)
            : this((ulong)(value >> HALF_BITS), (ulong)(value & ulong.MaxValue)) { }

        public override int GetHashCode()
            => HashCode.Combine(high, low);

        public override bool Equals(object obj)
            => (obj is UInt128 uint128) && Equals(uint128);

        public bool Equals(UInt128 other)
            => other.high == high && other.low == low;

        public int CompareTo(UInt128 other)
            => high != other.high ?
                high.CompareTo(other.high) :
                low.CompareTo(other.low);

        public string ToString(string format, IFormatProvider formatProvider)
        {
            string str = ((BigInteger)this).ToString(format, formatProvider);
            return (high >> 63 == 1 && str[0] == '0') ? str[1..] : str;
        }

        public string ToString(string format)
            => ToString(format, System.Globalization.CultureInfo.CurrentCulture);

        public override string ToString()
            => ToBigInteger().ToString();

        /* Arithmetic + Operator overloads */

        public static UInt128 operator &(UInt128 value1, UInt128 value2)
            => new(value1.high & value2.high, value1.low & value2.low);

        public static UInt128 operator |(UInt128 value1, UInt128 value2)
            => new(value1.high | value2.high, value1.low | value2.low);

        public static UInt128 operator ++(UInt128 value)
        {
            ulong T = value.low + 1;
            return new(value.high + (((value.low ^ T) & value.low) >> 63), T);
        }

        public static UInt128 operator --(UInt128 value)
        {
            ulong T = value.low - 1;
            return new(value.high - (((T ^ value.low) & T) >> 63), T);
        }

        public static UInt128 operator +(UInt128 value1, UInt128 value2)
        {
            ulong low = value1.low + value2.low,
                high = value1.high + value2.high,
                carry = low < Math.Max(value1.low, value2.low) ? 1ul : 0ul;
            return new(high + carry, low);
        }

        public static UInt128 operator -(UInt128 value1, UInt128 value2)
        {
            ulong low = value1.low - value2.low,
                high = value1.high - value2.high,
                carry = low > value1.low ? 1ul : 0ul;
            return new(high - carry, low);
        }

        public static UInt128 operator *(UInt128 value1, UInt128 value2)
        {
            Mult64To128(value1.low, value2.low, out ulong high, out ulong low);
            high += (value1.high * value2.low) + (value1.low * value2.high);
            return new(high, low);
        }

        public static UInt128 operator *(int i, UInt128 value)
            => MultInt(ref value, i);

        public static UInt128 operator *(UInt128 value, int i)
            => MultInt(ref value, i);

        public static UInt128 MultInt(ref UInt128 value, int i)
            => new(value.ToBigInteger() * i);

        public static UInt128 operator >>(UInt128 value, int offset)
        {
            if (offset >= MAX_BITS) return Zero;
            if (offset >= HALF_BITS) return new(0, value.high >> (offset - HALF_BITS));
            if (offset == 0) return value;
            return new(value.high >> offset, (value.low >> offset) + (value.high << (HALF_BITS - offset)));
        }

        public static UInt128 operator <<(UInt128 value, int offset)
        {
            offset %= MAX_BITS;
            if (offset >= HALF_BITS) return new(value.low << (offset - HALF_BITS), 0);
            if (offset == 0) return value;
            return new((value.high << offset) + (value.low >> (HALF_BITS - offset)), value.low << offset);
        }

        public static bool operator ==(UInt128 left, UInt128 right)
            => left.Equals(right);
        
        public static bool operator !=(UInt128 left, UInt128 right)
            => !(left == right);

        public static bool operator <(UInt128 left, UInt128 right)
            => left.CompareTo(right) < 0;

        public static bool operator <=(UInt128 left, UInt128 right)
            => left.CompareTo(right) <= 0;

        public static bool operator >(UInt128 left, UInt128 right)
            => left.CompareTo(right) > 0;

        public static bool operator >=(UInt128 left, UInt128 right)
            => left.CompareTo(right) >= 0;

        /* Adapted from C++ code : https://www.codeproject.com/Tips/618570/UInt-Multiplication-Squaring */
        public static void Mult64To128(ulong u, ulong v, out ulong high, out ulong low)
        {
            if (u == 0 || v == 0)
            {
                high = low = 0;
                return;
            }
            else if (u == 1 || v == 1)
            {
                high = 0;
                low = v;
                return;
            }
            ulong u1 = u & 0xffffffff;
            ulong v1 = v & 0xffffffff;
            ulong t = u1 * v1;
            ulong w3 = t & 0xffffffff;
            ulong k = t >> 32;

            u >>= 32;
            t = (u * v1) + k;
            k = t & 0xffffffff;
            ulong w1 = t >> 32;

            v >>= 32;
            t = (u1 * v) + k;
            k = t >> 32;

            high = (u * v) + w1 + k;
            low = (t << 32) + w3;
        }

        /* Conversions */

        public BigInteger ToBigInteger()
        {
            BigInteger value = high;
            value <<= HALF_BITS;
            value += low;
            return value;
        }

        public static explicit operator UInt128(BigInteger value)
            => new(value);

        public static implicit operator BigInteger(UInt128 value)
            => value.ToBigInteger();

        public static explicit operator ulong(UInt128 value)
            => value.low;

        public static implicit operator UInt128(ulong value)
            => new(0, value);
    }

    #endregion

    #region IPv6 structs and subnetting

    /* Reference: https://datatracker.ietf.org/doc/html/rfc5952 */
    public readonly struct IP6
    {
        public static readonly int MAX_BITS = 128,
            MAX_HEXTET = 0xFFFF, MIN_HEXTET = 0,
            HEXTETS = 8, HEXTET_BITS = 16;
        public static readonly char SEPARATOR = ':';
        public static readonly string SHORT_SEPARATOR = "::",
            HEX_FORMAT = "x", LONG_HEX_FORMAT = "x4";
        public static readonly UInt128 HEXTET_MASK = 0xFFFF;
        public static readonly IP6 MIN_IP = new(UInt128.Zero),
            MAX_IP = new(UInt128.MaxValue);

        private readonly UInt128 bits;

        public IP6(UInt128 bits)
            => this.bits = bits;

        public IP6(ulong high, ulong low)
            => bits = new UInt128(high, low);

        public static IP6 operator &(IP6 a, IP6 b)
            => new(bits: a.bits & b.bits);

        public static IP6 operator |(IP6 a, IP6 b)
            => new(bits: a.bits | b.bits);

        public static IP6 operator -(IP6 a, uint offset)
            => new(bits: a.bits - offset);

        public static UInt128 operator -(IP6 a, IP6 b)
            => a.bits - b.bits;

        public static IP6 operator +(IP6 a, uint offset)
            => new(bits: a.bits + offset);

        public static IP6 operator +(uint offset, IP6 a)
            => new(bits: a.bits + offset);

        public static IP6 operator +(IP6 a, IP6 b)
            => new(bits: a.bits + b.bits);

        public static IP6 operator +(IP6 a, UInt128 offset)
            => new(bits: a.bits + offset);

        public IEnumerable<ushort> Hextets()
        {
            for (int i = HEXTETS - 1; i >= 0; i--)
                yield return (ushort)((bits & (HEXTET_MASK << (HEXTET_BITS * i))) >> (HEXTET_BITS * i));
        }

        public IEnumerable<ValueTuple<int, ushort>> HextetsIndexed()
        {
            var i = 0;
            foreach (var hextet in Hextets())
                yield return (i++, hextet);
        }

        public string ToStringShort()
            => ToStringShort(HEX_FORMAT);

        public string ToStringLong()
            => ToString(this, LONG_HEX_FORMAT);

        public string ToStringSimple()
            => ToString(this, HEX_FORMAT);

        public override string ToString()
            => ToStringShort();

        public static string ToString(IP6 ip, string format)
        {
            var sb = new StringBuilder();
            foreach (ushort hextet in ip.Hextets())
                sb.Append(hextet.ToString(format)).Append(SEPARATOR);
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        /* Using rules defined in section 4 of RFC5952 */
        public string ToStringShort(string format)
        {
            // Count biggest group of zero hextets
            int max = 0, maxpos = 0,
                pos = -1, count = 0, 
                prev = -1;
            foreach ((var i, var hextet) in HextetsIndexed())
            {
                if (prev != 0 && hextet == 0)
                { 
                    pos = i;
                    count++;
                }
                else if (prev == 0 && prev == hextet)
                {
                    count++;
                    if (count > max) 
                    { 
                        max = count;
                        maxpos = pos;
                    }
                }
                else if (hextet != 0)
                    count = 0;
                prev = hextet;
            }
            if (max < 2)    // RFC section 4.2.2 says we can't shorten only one 0 hextet/nibble
                return ToStringSimple();
            // Display as string
            var sb = new StringBuilder();
            if (maxpos == 0)
                sb.Append(SEPARATOR);
            var size = max;
            foreach ((var i, var hextet) in HextetsIndexed())
            {
                if (i >= maxpos && max>0)
                {
                    max--;
                    if (max == 0)
                        sb.Append(SEPARATOR);
                }
                else 
                    sb.Append(hextet.ToString(format)).Append(SEPARATOR);
            }
            if (maxpos + size < HEXTETS)
                sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        public static IP6 From(string str)
        {
            string[] empty = Array.Empty<string>(), top, bottom = empty;
            if (str.Contains(SHORT_SEPARATOR))
            {
                var split = str.Split(SHORT_SEPARATOR);
                top = split[0].Equals("") ? empty : split[0].Split(SEPARATOR);
                bottom = split[1].Equals("") ? empty : split[1].Split(SEPARATOR);
            }
            else 
                top = str.Split(SEPARATOR);
            var filler = HEXTETS - (top.Length + bottom.Length);
            UInt128 bits = UInt128.Zero, hextet;
            for (int i = HEXTETS - 1; i >= 0; i--)
            {
                var n = HEXTETS - 1 - i;
                if (n < top.Length)
                    hextet = Convert.ToUInt16(top[n], 16);
                else if (top.Length <= n && n < top.Length + filler)
                    hextet = 0;
                else
                    hextet = Convert.ToUInt16(bottom[n - top.Length - filler]);
                bits |= hextet << (HEXTET_BITS * i);
            }
            return new(bits);
        }

        public readonly struct Range
        {
            public readonly IP6 From, To;
            public readonly UInt128 TotalAddresses;

            public Range(IP6 from, IP6 to)
            {
                From = from;
                To = to;
                TotalAddresses = to - from;
            }

            public override string ToString()
                => $"{From} -> {To}";
        }

        public readonly struct CIDR
        {
            public readonly int Prefix;
            public readonly IP6 IP;
            public readonly Range Range;

            public CIDR(IP6 ip, int prefix)
            {
                IP = ip;
                Prefix = prefix;
                var to = ip.bits + (UInt128.MaxValue >> prefix);
                Range = new Range(ip, new IP6(to));
            }

            public static CIDR From(string str)
            {
                if (!str.Contains("/"))
                    throw new ArgumentException("Prefix not found");
                var split = str.Split("/");
                return new(IP6.From(split[0]), Convert.ToInt32(split[1]));
            }

            public UInt128 MaximumSubnets(int prefix)
            {
                var power = MAX_BITS - Prefix - prefix;
                if (power < 0)
                    return 0;
                return UInt128.One << power;
            }

            public override string ToString()
                => $"{IP}/{Prefix}";
        }

        /* IPv6 subnets are defined as hierarchies, so I guess it's more
         * memory efficient to just calculate the address on-the-fly 
         * based on specific indices inside each subnet.
         */
        public readonly struct Subnet
        {
            public readonly CIDR CIDR;
            public readonly int Levels;
            public readonly string[] Names;
            public readonly int[] Subnets;         // size of each subnet
            public readonly int[] Bits;             // bits used for each subnet
            public readonly UInt128[] Increments;   // each increment per subnet

            public Subnet(CIDR cidr, int levels, string[] names, int[] subnets, int[] bits, UInt128[] increments)
            {
                CIDR = cidr;
                Levels = levels;
                Names = names;
                Subnets = subnets;
                Bits = bits;
                Increments = increments;
            }

            /* By using an Indexer it feels more natural to access
             * each subnet from the hierarchy; especially since we
             * have the equivalent of a strides array (e.g. Increments
             * for the address and Bits for the prefix respectively).
             */
            public CIDR this[params int[] indices]
            {
                get
                {
                    if (indices.Length == 0)
                        return CIDR;
                    if (indices.Length > Levels)
                        throw new ArgumentException("Too many indices");
                    var ip = CIDR.IP;
                    var prefix = CIDR.Prefix;
                    for (int i=0; i<indices.Length; i++)
                    {
                        ip += indices[i] * Increments[i];
                        prefix += Bits[i];
                    }
                    return new(ip, prefix);
                }
            }

        }

        public readonly struct VLSM
        {
            public static Subnet For(CIDR cidr, Dictionary<string, int> table)
            {
                var names = new string[table.Count];
                var sizes = new int[table.Count];
                var bits = new int[table.Count];
                var increments = new UInt128[table.Count];
                var i = 0;
                int usedBits = 0;
                foreach ((var name, var subnets) in table)
                {
                    bits[i] = NearestPowerOf2((ulong)subnets);
                    names[i] = name;
                    sizes[i] = subnets;
                    usedBits += bits[i];
                    increments[i] = UInt128.One << (MAX_BITS - cidr.Prefix - usedBits);
                    i++;
                }
                return new Subnet(cidr, table.Count, names, sizes, bits, increments);
            }

            public static int NearestPowerOf2(ulong val)
            {
                var i = 0;
                while (UInt128.One << i < val)
                    i++;
                return i;
            }
        }
    }

    #endregion

    class Program
    {
        public static void Main(string[] args)
        {
            //TestIP4();
            //TestUint128();
            TestIP6();
        }

        public static void TestIP4()
        {
            //var ip = IP4.From("112.3.2.3");
            //Console.WriteLine(ip);
            //Console.WriteLine(IP4.CIDR.From("112.3.2.3/25"));

            //foreach (byte octet in ip.Octets())
            //    Console.WriteLine(octet);

            var ip = IP4.From("192.168.0.0");   // class C
            var dict = new Dictionary<string, uint>
            {
                { "IT", 275 },
                { "Accounting", 127 },
                { "Sales", 265 },
                { "Marketing", 400 },
                { "Customer Care", 38 }
            };
            var subnets = IP4.VLSM.For(ip, dict);
            foreach (var subnet in subnets)
                Console.WriteLine(subnet);
        }

        public static void TestUint128()
        {
            UInt128 max = UInt128.MaxValue;
            Console.WriteLine(max);

            //Console.WriteLine(max + 1);         // overflow
            //Console.WriteLine(max >> UInt128.HALF_BITS << UInt128.HALF_BITS);   // loses precision from truncation

            UInt128 mask = 0xff;
            for (int i = 0; i < UInt128.BYTES; i++)
                Console.WriteLine($"Byte #{i} : {(max & (mask << (i * 8))) >> (i * 8)}");   // extract all 16 bytes to check
        }

        public static void TestIP6()
        {
            //uint sixteen = 0b0010_0000_0000_0001;
            //Console.WriteLine(sixteen.ToString("X"));

            //var ip = new IP6(new UInt128(0x20010db82231aaec, 0x4a4a2100));    // 2001:0db8:2231:aaec:0000:0000:4a4a:2100 == 2001:db8:2231:aaec::4a4a:2100
            //var ip = new IP6(0x20010db800000000, 0x0000000000020001);         // 2001:db8:0:0:0:0:2:1 == 2001:db8::2:1
            //var ip = new IP6(0x20010db800000001, 0x0001000100010001);         // 2001:db8:0:1:1:1:1:1
            //var ip = new IP6(0x0, 0x1);                                       // ::1
            //var ip = new IP6(0x20010db82231aaec, 0x0);                        // 2001:0db8:2231:aaec::
            //Console.WriteLine(ip.ToString());
            //foreach ((var i, var hextet) in ip.HextetsIndexed())
            //    Console.WriteLine($"{i}: {hextet:x}");

            //var ip = IP6.From("2001:db8:0:1:1:1:1:1");
            //var ip = IP6.From("2001::1");
            //var ip = IP6.From("2001::");
            //var ip = IP6.From("::1");
            //Console.WriteLine(ip);

            //var cidr = IP6.CIDR.From("::/0");
            //Console.WriteLine(cidr);
            //Console.WriteLine(cidr.Range.TotalAddresses);
            //Console.WriteLine(cidr.MaximumSubnets(64));     // calculates how many /64s can we have 

            var cidr = IP6.CIDR.From("2001:db8:cad::/48");  // given by ISP
            Console.WriteLine($"Initial CIDR: {cidr}");
            var dict = new Dictionary<string, int>          // subnet hierarchy
            {
                { "Country", 3 },
                { "State", 60 },
                { "Office", 10 }
            };
            // Print subnets hierarchy
            var subnet = IP6.VLSM.For(cidr, dict);
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine($"{subnet.Names[0]} {i}: {subnet[i]}");
                for (int j = 0; j < 60; j++)
                {
                    Console.WriteLine($"|---{subnet.Names[1]} {j}: {subnet[i, j]}");
                    for (int k = 0; k < 10; k++)
                        Console.WriteLine($"|   |---{subnet.Names[2]} {k}: {subnet[i, j, k]}");
                }
            }
                
        }
    }
}