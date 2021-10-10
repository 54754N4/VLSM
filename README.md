# VLSM Subnetter

By just specifying the total hosts required in each VLAN

```C#
var dict = new Dictionary<string, uint>
{
    { "IT", 275 },
    { "Accounting", 127 },
    { "Sales", 265 },
    { "Marketing", 400 },
    { "Customer Care", 38 }
};
var startIP = IP4.From("192.168.0.0");    // we're using class C private networks
var subnets = VLSM.Of(dict, startIP);
foreach (var subnet in subnets)
    Console.WriteLine(subnet);
```

Subnetter will automatically calculate the respective configurations for each as such :

| Subnet | Hosts | Subnetmask | Network Address | Broadcast Address | IP Range |
|  :----:  | :----: | :----: | :----: | :----: | :----: |
| Marketing | 400 | 255.255.254.0 | 192.168.0.0 | 192.168.1.255 | 192.168.0.1-192.168.1.254 |
| IT | 275 | 255.255.254.0 | 192.168.2.0 | 192.168.3.255 | 192.168.2.1-192.168.3.254 |
| Sales | 265 | 255.255.254.0 | 192.168.4.0 | 192.168.5.255 | 192.168.4.1-192.168.5.254 |
| Accounting | 127 | 255.255.255.0 | 192.168.6.0 | 192.168.6.255 | 192.168.6.1-192.168.6.254 |
| Customer Care | 38 | 255.255.255.192 | 192.168.7.0 | 192.168.7.63 | 192.168.7.1-192.168.7.62 |
