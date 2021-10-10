using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Subnetter
{
    public readonly struct IP4
    {
        public static readonly int MAX_BITS = 32,
            MAX_OCTET = 255, MIN_OCTET = 0,
            OCTET_BITS = 8, OCTETS = 4;

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

        public static IP4 operator +(uint offset, IP4 a)
            => new(bits: a.bits + offset);

        public static IP4 operator -(IP4 a, uint offset)
            => new(bits: a.bits - offset);

        public override string ToString()
        {
            var sb = new StringBuilder();
            uint octet;
            for (int i = OCTETS - 1; i >= 0; i--)
            {
                octet = (bits & (0xFFu << (OCTET_BITS * i))) >> (OCTET_BITS * i);
                sb.Append(octet).Append('.');
            }
            sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        public static IP4 From(string str)
        {
            var split = str.Split('.');
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
            return new IP4(bits);
        }

        public static IP4 SubnetMask(int prefix)
        {
            int leftover = MAX_BITS - prefix;
            uint ones = (1u << prefix) - 1;
            return new IP4(ones << leftover);
        }

    }

    public readonly struct CIDR
    {
        public readonly int NetworkBits, HostBits, MaximumHosts, MaximumSubnets;
        public readonly IP4 SubnetMask, WildcardMask, NetworkAddress, BroadcastAddress;

        public CIDR(IP4 ip, int prefix)
        {
            if (prefix < 0 || prefix > 32)
                throw new ArgumentException("Invalid CIDR prefix");
            SubnetMask = IP4.SubnetMask(prefix);
            NetworkBits = prefix;
            HostBits = IP4.MAX_BITS - prefix;
            MaximumHosts = 1 << HostBits;
            MaximumSubnets = 1 << NetworkBits;
            WildcardMask = ~SubnetMask;
            NetworkAddress = ip & SubnetMask;
            BroadcastAddress = ip | WildcardMask;
        }

        public override string ToString()
            => $"Network bits: {NetworkBits}\n" +
                $"Host bits: {HostBits}\n" +
                $"Maximum hosts per subnet: {MaximumHosts} ({MaximumHosts-2} valid hosts)\n" +  // -2 to exclude network address and broadcast address
                $"Maximum subnets per network: {MaximumSubnets}\n" +
                $"Subnetmask: {SubnetMask}\n" +
                $"Wildcardmask: {WildcardMask}\n" +
                $"Network address: {NetworkAddress}\n" +
                $"Broadcast address: {BroadcastAddress}\n";

        public static CIDR From(string cidr)
        {
            if (!cidr.Contains('/'))
                throw new ArgumentException("Prefix not found");
            var split = cidr.Split('/');
            return new(IP4.From(split[0]), int.Parse(split[1]));
        }
    }

    public readonly struct IP4Range
    {
        public readonly IP4 from, to;

        public IP4Range(IP4 from, IP4 to)
        {
            this.from = from;
            this.to = to;
        }

        public override string ToString()
            => $"{from}-{to}";
    }

    public readonly struct Subnet
    {
        public readonly string Name;
        public readonly uint Hosts;
        public readonly IP4 SubnetMask, NetworkAddress, BroadcastAddress;
        public readonly IP4Range Range;

        public Subnet(string Name, uint Hosts, IP4 SubnetMask, IP4 NetworkAddress, IP4 BroadcastAddress, IP4Range Range)
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
        public static IList<Subnet> Of(Dictionary<string, uint> table, IP4 start)
        {
            var subnets = new List<Subnet>();
            var ip = start;
            foreach (var entry in table.OrderByDescending(x => x.Value))
            {
                var hostBits = NearestPowerOf2(entry.Value+2);  // account for the network + broadcast addresses
                var networkBits = IP4.MAX_BITS - hostBits;
                var sm = IP4.SubnetMask(networkBits);
                var nw = ip & sm;
                var bc = ip | ~sm;
                var range = new IP4Range(nw + 1, bc - 1);
                subnets.Add(new Subnet(entry.Key, entry.Value, sm, nw, bc, range));
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

    class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine(IP4.From("112.3.2.3"));
            //Console.WriteLine(CIDR.From("112.3.2.3/25"));
            var dict = new Dictionary<string, uint>
            {
                { "IT", 275 },
                { "Accounting", 127 },
                { "Sales", 265 },
                { "Marketing", 400 },
                { "Customer Care", 38 }
            };
            var subnets = VLSM.Of(dict, IP4.From("192.168.0.0"));
            foreach (var subnet in subnets)
                Console.WriteLine(subnet);
        }
    }
}