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

```
Subnet name: Marketing
Hosts: 400
Subnetmask: 255.255.254.0
Network address: 192.168.0.0
Broadcast address: 192.168.1.255
IP range: 192.168.0.1-192.168.1.254

Subnet name: IT
Hosts: 275
Subnetmask: 255.255.254.0
Network address: 192.168.2.0
Broadcast address: 192.168.3.255
IP range: 192.168.2.1-192.168.3.254

Subnet name: Sales
Hosts: 265
Subnetmask: 255.255.254.0
Network address: 192.168.4.0
Broadcast address: 192.168.5.255
IP range: 192.168.4.1-192.168.5.254

Subnet name: Accounting
Hosts: 127
Subnetmask: 255.255.255.0
Network address: 192.168.6.0
Broadcast address: 192.168.6.255
IP range: 192.168.6.1-192.168.6.254

Subnet name: Customer Care
Hosts: 38
Subnetmask: 255.255.255.192
Network address: 192.168.7.0
Broadcast address: 192.168.7.63
IP range: 192.168.7.1-192.168.7.62
```
