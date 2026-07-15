namespace ModularCommerce.Discovery.Contracts;

/// <summary>
/// Assembly çapa tipi. Discovery'yi başka modül tüketmediğinden (arama yalnız HTTP ile dışa açık)
/// bu sözleşme projesi bugün boştur; tip, mimari testlerin assembly'yi yükleyebilmesi ve ileride
/// bir cross-module sözleşme eklendiğinde hazır bir yuva olması içindir.
/// </summary>
public interface IDiscoveryContractsMarker;
