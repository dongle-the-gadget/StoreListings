using StoreListings.Library.Internal;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace StoreListings.Library;

public static partial class FE3Handler
{
    public class Cookie
    {
        public required string CookieData { get; set; }

        public required string Expiration { get; set; }
    }

    private class InternalSyncUpdatesResponse
    {
        public required Dictionary<string, string> NewUpdatesInfo { get; set; }

        public required Dictionary<string, string> ExtendedUpdatesInfo { get; set; }
    }

    public class SyncUpdatesResponse
    {
        public class Update
        {
            public class Platform
            {
                public required Version MinVersion { get; set; }

                public required DeviceFamily Family { get; set; }
            }

            public required string FileName { get; set; }

            public required string UpdateID { get; set; }

            public required string RevisionNumber { get; set; }

            public required string Digest { get; set; }

            public required string PackageIdentityName { get; set; }

            public required Version Version { get; set; }

            public required IEnumerable<Platform> TargetPlatforms { get; set; }
        }

        public required IEnumerable<Update> Updates { get; set; }

        public required Cookie NewCookie { get; set; }
    }

    public static async Task<Result<Cookie>> GetCookieAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string cookie = $"""
                <Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns="http://www.w3.org/2003/05/soap-envelope">
                    <Header>
                        <Action d3p1:mustUnderstand="1" xmlns:d3p1="http://www.w3.org/2003/05/soap-envelope" xmlns="http://www.w3.org/2005/08/addressing">http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService/GetCookie</Action>
                        <MessageID xmlns="http://www.w3.org/2005/08/addressing">urn:uuid:{Guid.NewGuid()}</MessageID>
                        <To d3p1:mustUnderstand="1" xmlns:d3p1="http://www.w3.org/2003/05/soap-envelope" xmlns="http://www.w3.org/2005/08/addressing">https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx</To>
                        <Security d3p1:mustUnderstand="1" xmlns:d3p1="http://www.w3.org/2003/05/soap-envelope" xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
                            <Timestamp xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
                                <Created>{DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")}</Created>
                                <Expires>2044-08-02T20:09:03Z</Expires>
                            </Timestamp>
                            <WindowsUpdateTicketsToken d4p1:id="ClientMSA" xmlns:d4p1="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd" xmlns="http://schemas.microsoft.com/msus/2014/10/WindowsUpdateAuthorization"></WindowsUpdateTicketsToken>
                        </Security>
                    </Header>
                    <Body>
                        <GetCookie xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
                            <oldCookie>
                                <Expiration>2016-07-27T07:18:09Z</Expiration>
                            </oldCookie>
                            <lastChange>2015-10-21T17:01:07.1472913Z</lastChange>
                            <currentTime>{DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")}</currentTime>
                            <protocolVersion>2.50</protocolVersion>
                        </GetCookie>
                    </Body>
                </Envelope>
                """;

            HttpClient client = Helpers.GetFE3StoreHttpClient();
            using HttpResponseMessage response = await client.PostAsync("https://fe3cr.delivery.mp.microsoft.com/ClientWebService/client.asmx", new StringContent(cookie, Encoding.UTF8, "application/soap+xml"), cancellationToken);
            response.EnsureSuccessStatusCode();
            XElement doc = await XElement.LoadAsync(await response.Content.ReadAsStreamAsync(), LoadOptions.None, cancellationToken);
            XElement body = doc.Element(XName.Get("Body", "http://www.w3.org/2003/05/soap-envelope"))!;
            XElement getCookieResponse = body.Element(XName.Get("GetCookieResponse", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!;
            XElement getCookieResult = getCookieResponse.Element(XName.Get("GetCookieResult", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!;

            XElement cookieData = getCookieResult.Element(XName.Get("EncryptedData", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!;
            XElement expiration = getCookieResult.Element(XName.Get("Expiration", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!;

            return Result<Cookie>.Success(new Cookie
            {
                CookieData = cookieData.Value,
                Expiration = expiration.Value
            });
        }
        catch (Exception ex)
        {
            return Result<Cookie>.Failure(ex);
        }
    }

    public static async Task<Result<SyncUpdatesResponse>> SyncUpdatesAsync(Cookie cookie, string WuCategoryId, Lang lang, Market market, string currentBranch, string flightRing, string flightingBranchName, Version OSVersion, DeviceFamily deviceFamily, CancellationToken cancellationToken = default)
    {
        try
        {
            HttpClient client = Helpers.GetFE3StoreHttpClient();
            XElement doc;
            string content;
            Cookie currentCookie = cookie;
            List<InternalSyncUpdatesResponse> responses = new();
            HashSet<string> FoundUpdateIDs = new();

            while (true)
            {
                content = GenerateSyncUpdatesPayload(cookie, WuCategoryId, lang, market, currentBranch, flightRing, flightingBranchName, OSVersion, deviceFamily, FoundUpdateIDs, FoundUpdateIDs);
                using HttpResponseMessage response = await client.PostAsync("https://fe3cr.delivery.mp.microsoft.com/ClientWebService/client.asmx", new StringContent(content, Encoding.UTF8, "application/soap+xml"), cancellationToken);
                response.EnsureSuccessStatusCode();
                doc = await XElement.LoadAsync(await response.Content.ReadAsStreamAsync(), LoadOptions.None, cancellationToken);

                XElement result = 
                    doc
                    .Element(XName.Get("Body", "http://www.w3.org/2003/05/soap-envelope"))!
                    .Element(XName.Get("SyncUpdatesResponse", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!
                    .Element(XName.Get("SyncUpdatesResult", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!;

                XElement newCookieXml = result.Element(XName.Get("NewCookie", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!;

                currentCookie = new Cookie()
                {
                    CookieData = newCookieXml.Element(XName.Get("EncryptedData", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!.Value,
                    Expiration = newCookieXml.Element(XName.Get("Expiration", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!.Value
                };

                XElement? extendedUpdatesInfo = result.Element(XName.Get("ExtendedUpdateInfo", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"));

                int extendedUpdatesNum;

                if (extendedUpdatesInfo is not null &&
                    extendedUpdatesInfo.Element(XName.Get("Updates", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!.Elements(XName.Get("Update", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService")) is { } updates &&
                    (extendedUpdatesNum = updates.Count()) > 0)
                {
                    IEnumerable<XElement> newUpdates =
                        result
                        .Element(XName.Get("NewUpdates", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!
                        .Elements(XName.Get("UpdateInfo", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"));
                    Dictionary<string, string> NewUpdateInfo = new(newUpdates.Count());
                    Dictionary<string, string> ExtendedUpdatesInfo = new(extendedUpdatesNum);
                    foreach (XElement update in updates)
                    {
                        string id = update.Element(XName.Get("ID", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!.Value;
                        FoundUpdateIDs.Add(id);
                        ExtendedUpdatesInfo.Add(id, update.Element(XName.Get("Xml", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!.Value);
                    }

                    foreach (XElement newUpdate in newUpdates)
                    {
                        string id = newUpdate.Element(XName.Get("ID", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!.Value;
                        NewUpdateInfo.Add(id, newUpdate.Element(XName.Get("Xml", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!.Value);
                    }

                    responses.Add(new InternalSyncUpdatesResponse()
                    {
                        ExtendedUpdatesInfo = ExtendedUpdatesInfo,
                        NewUpdatesInfo = NewUpdateInfo
                    });
                }
                else
                {
                    break;
                }
            }

            List<SyncUpdatesResponse.Update> updateResponses = new();

            foreach (InternalSyncUpdatesResponse response in responses)
            {
                foreach (KeyValuePair<string, string> extendedUpdateInfo in response.ExtendedUpdatesInfo)
                {
                    string newUpdateInfo = response.NewUpdatesInfo[extendedUpdateInfo.Key];
                    if (!extendedUpdateInfo.Value.Contains("<Files") || !newUpdateInfo.Contains("SecuredFragment"))
                        continue;
                    string appendedUpdateInfoXml = $"<Xml>{extendedUpdateInfo.Value}{newUpdateInfo}</Xml>";
                    doc = XElement.Parse(appendedUpdateInfoXml);

                    XElement updateIdentity = doc.Element("UpdateIdentity")!;
                    string updateId = updateIdentity.Attribute("UpdateID")!.Value;
                    string revisionNumber = updateIdentity.Attribute("RevisionNumber")!.Value;

                    XElement extendedProperties = doc.Element("ExtendedProperties")!;
                    string packageIdentityName = extendedProperties.Attribute("PackageIdentityName")!.Value;

                    List<SyncUpdatesResponse.Update.Platform> platforms;

                    using (JsonDocument jsonApplicabilityDoc = JsonDocument.Parse(doc.Element("ApplicabilityRules")!.Element("Metadata")!.Element("AppxPackageMetadata")!.Element("AppxMetadata")!.Element("ApplicabilityBlob")!.Value))
                    {
                        if (!jsonApplicabilityDoc.RootElement.TryGetProperty("content.targetPlatforms", out JsonElement targetPlatforms))
                            continue; // Windows 8, not yet supported.
                        int numPlatforms = targetPlatforms.GetArrayLength();
                        platforms = new(numPlatforms);
                        for (int i = 0; i < numPlatforms; i++)
                        {
                            JsonElement targetPlatform = targetPlatforms[i];
                            SyncUpdatesResponse.Update.Platform platform = new()
                            {
                                Family = ConvertFE3PlatformToDeviceFamily(targetPlatform.GetProperty("platform.target").GetInt64()),
                                MinVersion = Version.FromWindowsRepresentation(targetPlatform.GetProperty("platform.minVersion").GetUInt64())
                            };
                            platforms.Add(platform);
                        }
                    }

                    foreach (XElement file in doc.Element("Files")!.Elements("File"))
                    {
                        string originalName = file.Attribute("FileName")!.Value;
                        if (originalName.EndsWith(".cab"))
                            continue;
                        string identifier = file.Attribute("InstallerSpecificIdentifier")!.Value;
                        string digest = file.Attribute("Digest")!.Value;
                        int extensionIndex = originalName.AsSpan().LastIndexOf('.');
                        string realName = $"{identifier}{originalName[extensionIndex..]}";

                        int firstIndex = identifier.AsSpan().IndexOf('_') + 1;
                        int secondIndex = identifier.AsSpan()[firstIndex..].IndexOf('_');

                        updateResponses.Add(new SyncUpdatesResponse.Update()
                        {
                            PackageIdentityName = packageIdentityName,
                            FileName = realName,
                            UpdateID = updateId,
                            Digest = digest,
                            RevisionNumber = revisionNumber,
                            Version = Version.Parse(identifier.AsSpan()[firstIndex..][..secondIndex], null),
                            TargetPlatforms = platforms
                        });
                    }
                }
            }

            return Result<SyncUpdatesResponse>.Success(new SyncUpdatesResponse()
            {
                NewCookie = currentCookie,
                Updates = updateResponses
            });
        }
        catch (Exception ex)
        {
            return Result<SyncUpdatesResponse>.Failure(ex);
        }
    }

    private static string GenerateSyncUpdatesPayload(Cookie cookie, string WuCategoryId, Lang lang, Market market, string currentBranch, string flightRing, string flightingBranchName, Version OSVersion, DeviceFamily deviceFamily, IEnumerable<string> additionalInstalledNonLeafUpdateIDs, IEnumerable<string> additionalOtherCachedUpdateIDs)
    {
        int flightEnabled = flightRing == "Retail" ? 0 : 1;
        string installType = deviceFamily switch
        {
            DeviceFamily.IoTUAP => "IoTUAP",
            DeviceFamily.Iot => "IoTUAP", // Not too sure about this one
            DeviceFamily.Server => "Server",
            DeviceFamily.Holographic => "FactoryOS",
            DeviceFamily.Core => "FactoryOS",
            _ => "Client"
        };
        string cached = string.Join(Environment.NewLine, additionalOtherCachedUpdateIDs.Select(x => $"<int>{x}</int>"));
        string nonleaf = string.Join(Environment.NewLine, additionalInstalledNonLeafUpdateIDs.Select(x => $"<int>{x}</int>"));
        return
            $"""
            <s:Envelope xmlns:a="http://www.w3.org/2005/08/addressing" xmlns:s="http://www.w3.org/2003/05/soap-envelope">
              <s:Header>
                <a:Action s:mustUnderstand="1">http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService/SyncUpdates</a:Action>
                <a:MessageID>urn:uuid:{Guid.NewGuid()}</a:MessageID>
                <a:To s:mustUnderstand="1">https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx</a:To>
                <o:Security s:mustUnderstand="1" xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
                  <Timestamp xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
                    <Created>{DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")}</Created>
                    <Expires>2044-08-02T20:09:03Z</Expires>
                  </Timestamp>
                  <wuws:WindowsUpdateTicketsToken wsu:id="ClientMSA" xmlns:wsu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd" xmlns:wuws="http://schemas.microsoft.com/msus/2014/10/WindowsUpdateAuthorization"></wuws:WindowsUpdateTicketsToken>
                </o:Security>
              </s:Header>
              <s:Body>
                <SyncUpdates xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
                  <cookie>
                    <Expiration>{cookie.Expiration}</Expiration>
                    <EncryptedData>{cookie.CookieData}</EncryptedData>
                  </cookie>
                  <parameters>
                    <ExpressQuery>false</ExpressQuery>
                    <InstalledNonLeafUpdateIDs>
                      <int>1</int>
                      <int>2</int>
                      <int>3</int>
                      <int>10</int>
                      <int>11</int>
                      <int>17</int>
                      <int>19</int>
                      <int>2359974</int>
                      <int>2359977</int>
                      <int>5143990</int>
                      <int>5169043</int>
                      <int>5169044</int>
                      <int>5169047</int>
                      <int>8788830</int>
                      <int>8806526</int>
                      <int>9125350</int>
                      <int>9154769</int>
                      <int>10809856</int>
                      <int>23110993</int>
                      <int>23110994</int>
                      <int>23110995</int>
                      <int>23110996</int>
                      <int>23110999</int>
                      <int>23111000</int>
                      <int>23111001</int>
                      <int>23111002</int>
                      <int>23111003</int>
                      <int>23111004</int>
                      <int>24513870</int>
                      <int>28880263</int>
                      <int>30077688</int>
                      <int>30486944</int>
                      <int>59830006</int>
                      <int>59830007</int>
                      <int>59830008</int>
                      <int>60484010</int>
                      <int>62450018</int>
                      <int>62450019</int>
                      <int>62450020</int>
                      <int>98959022</int>
                      <int>98959023</int>
                      <int>98959024</int>
                      <int>98959025</int>
                      <int>98959026</int>
                      <int>105939029</int>
                      <int>105995585</int>
                      <int>106017178</int>
                      <int>107825194</int>
                      <int>117765322</int>
                      <int>129905029</int>
                      <int>130040030</int>
                      <int>130040031</int>
                      <int>130040032</int>
                      <int>130040033</int>
                      <int>133399034</int>
                      <int>138372035</int>
                      <int>138372036</int>
                      <int>139536037</int>
                      <int>139536038</int>
                      <int>139536039</int>
                      <int>139536040</int>
                      <int>142045136</int>
                      <int>158941041</int>
                      <int>158941042</int>
                      <int>158941043</int>
                      <int>158941044</int>
                      <int>159776047</int>
                      <int>160733048</int>
                      <int>160733049</int>
                      <int>160733050</int>
                      <int>160733051</int>
                      <int>160733055</int>
                      <int>160733056</int>
                      <int>161870057</int>
                      <int>161870058</int>
                      <int>161870059</int>
                      {nonleaf}
                    </InstalledNonLeafUpdateIDs>
                    <OtherCachedUpdateIDs>
                      {cached}
                    </OtherCachedUpdateIDs>
                    <SkipSoftwareSync>false</SkipSoftwareSync>
                    <NeedTwoGroupOutOfScopeUpdates>true</NeedTwoGroupOutOfScopeUpdates>
                    <FilterAppCategoryIds>
                      <CategoryIdentifier>
                        <Id>{WuCategoryId}</Id>
                      </CategoryIdentifier>
                    </FilterAppCategoryIds>
                    <TreatAppCategoryIdsAsInstalled>true</TreatAppCategoryIdsAsInstalled>
                    <AlsoPerformRegularSync>false</AlsoPerformRegularSync>
                    <ComputerSpec />
                    <ExtendedUpdateInfoParameters>
                      <XmlUpdateFragmentTypes>
                        <XmlUpdateFragmentType>Extended</XmlUpdateFragmentType>
                        <XmlUpdateFragmentType>Published</XmlUpdateFragmentType>
                        <XmlUpdateFragmentType>Core</XmlUpdateFragmentType>
                      </XmlUpdateFragmentTypes>
                      <Locales>
                        <string>{lang}-{market}</string>
                        <string>{lang}</string>
                      </Locales>
                    </ExtendedUpdateInfoParameters>
                    <ClientPreferredLanguages>
                      <string>{lang}-{market}</string>
                    </ClientPreferredLanguages>
                    <ProductsParameters>
                      <SyncCurrentVersionOnly>false</SyncCurrentVersionOnly>
                      <DeviceAttributes>BranchReadinessLevel=CB;CurrentBranch={currentBranch};OEMModel=Virtual Machine;FlightRing={flightRing};AttrDataVer=21;SystemManufacturer=Microsoft Corporation;InstallLanguage={lang}-{market};OSUILocale={lang}-{market};InstallationType={installType};FlightingBranchName={flightingBranchName};FirmwareVersion=Hyper-V UEFI Release v2.5;SystemProductName=Virtual Machine;OSSkuId=48;FlightContent=Mainline;App=WU_STORE;OEMName_Uncleaned=Microsoft Corporation;AppVer=0.0.0.0;OSArchitecture=AMD64;SystemSKU=None;UpdateManagementGroup=2;IsFlightingEnabled={flightEnabled};IsDeviceRetailDemo=0;TelemetryLevel=3;OSVersion={OSVersion};DeviceFamily=Windows.{deviceFamily};</DeviceAttributes>
                      <CallerAttributes>Interactive=1;IsSeeker=0;</CallerAttributes>
                      <Products />
                    </ProductsParameters>
                  </parameters>
                </SyncUpdates>
              </s:Body>
            </s:Envelope>
            """;
    }

    public static DeviceFamily ConvertFE3PlatformToDeviceFamily(long platform) => platform switch
    {
        0 => DeviceFamily.Universal,
        3 => DeviceFamily.Desktop,
        4 => DeviceFamily.Mobile,
        5 => DeviceFamily.Xbox,
        6 => DeviceFamily.Team,
        10 => DeviceFamily.Holographic,
        16 => DeviceFamily.Core,
        _ => DeviceFamily.Unknown
    };

    public static async Task<Result<string>> GetFileUrl(Cookie cookie, string updateID, string revisionNumber, string digest, Lang lang, Market market, string currentBranch, string flightRing, string flightingBranchName, Version OSVersion, DeviceFamily deviceFamily, CancellationToken cancellationToken = default)
    {
        try
        {
            int flightEnabled = flightRing == "Retail" ? 0 : 1;
            string installType = deviceFamily switch
            {
                DeviceFamily.IoTUAP => "IoTUAP",
                DeviceFamily.Iot => "IoTUAP", // Not too sure about this one
                DeviceFamily.Server => "Server",
                DeviceFamily.Holographic => "FactoryOS",
                DeviceFamily.Core => "FactoryOS",
                _ => "Client"
            };

            string content = $"""
            <s:Envelope
            	xmlns:a="http://www.w3.org/2005/08/addressing"
            	xmlns:s="http://www.w3.org/2003/05/soap-envelope">
                <s:Header>
                    <a:Action s:mustUnderstand="1">http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService/GetExtendedUpdateInfo2</a:Action>
                    <a:MessageID>urn:uuid:{Guid.NewGuid()}</a:MessageID>
                    <a:To s:mustUnderstand="1">https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx/secured</a:To>
                    <o:Security s:mustUnderstand="1"
            			xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
                        <Timestamp
            				xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
                            <Created>{DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'")}</Created>
                            <Expires>2044-08-02T20:09:03Z</Expires>
                        </Timestamp>
                        <wuws:WindowsUpdateTicketsToken wsu:id="ClientMSA" xmlns:wsu="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd" xmlns:wuws="http://schemas.microsoft.com/msus/2014/10/WindowsUpdateAuthorization"></wuws:WindowsUpdateTicketsToken>
                    </o:Security>
                </s:Header>
                <s:Body>
                    <GetExtendedUpdateInfo2
            			xmlns="http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService">
                        <updateIDs>
                            <UpdateIdentity>
                                <UpdateID>{updateID}</UpdateID>
                                <RevisionNumber>{revisionNumber}</RevisionNumber>
                            </UpdateIdentity>
                        </updateIDs>
                        <infoTypes>
                            <XmlUpdateFragmentType>FileUrl</XmlUpdateFragmentType>
                            <XmlUpdateFragmentType>FileDecryption</XmlUpdateFragmentType>
                        </infoTypes>
                        <deviceAttributes>BranchReadinessLevel=CB;CurrentBranch={currentBranch};OEMModel=Virtual Machine;FlightRing={flightRing};AttrDataVer=21;SystemManufacturer=Microsoft Corporation;InstallLanguage={lang}-{market};OSUILocale={lang}-{market};InstallationType={installType};FlightingBranchName={flightingBranchName};FirmwareVersion=Hyper-V UEFI Release v2.5;SystemProductName=Virtual Machine;OSSkuId=48;FlightContent=Mainline;App=WU_STORE;OEMName_Uncleaned=Microsoft Corporation;AppVer=0.0.0.0;OSArchitecture=AMD64;SystemSKU=None;UpdateManagementGroup=2;IsFlightingEnabled={flightEnabled};IsDeviceRetailDemo=0;TelemetryLevel=3;OSVersion={OSVersion};DeviceFamily=Windows.{deviceFamily};</deviceAttributes>
                    </GetExtendedUpdateInfo2>
                </s:Body>
            </s:Envelope>
            """;

            HttpClient client = Helpers.GetFE3StoreHttpClient();
            using HttpResponseMessage response = await client.PostAsync("https://fe3cr.delivery.mp.microsoft.com/ClientWebService/client.asmx/secured", new StringContent(content, Encoding.UTF8, "application/soap+xml"), cancellationToken);
            response.EnsureSuccessStatusCode();
            XElement doc = await XElement.LoadAsync(await response.Content.ReadAsStreamAsync(), LoadOptions.None, cancellationToken);
            XElement body = doc.Element(XName.Get("Body", "http://www.w3.org/2003/05/soap-envelope"))!;

            XElement getExtendedUpdateInfo2Response = body.Element(XName.Get("GetExtendedUpdateInfo2Response", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!;
            XElement getExtendedUpdateInfo2Result = getExtendedUpdateInfo2Response.Element(XName.Get("GetExtendedUpdateInfo2Result", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!;
            XElement fileLocations = getExtendedUpdateInfo2Result.Element(XName.Get("FileLocations", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!;
            IEnumerable<XElement> listOfLocations = fileLocations.Elements(XName.Get("FileLocation", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!;

            foreach (XElement fileLocation in listOfLocations)
            {
                string fileDigest = fileLocation.Element(XName.Get("FileDigest", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!.Value;
                if (fileDigest != digest)
                    continue;
                string fileUrl = fileLocation.Element(XName.Get("Url", "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService"))!.Value;
                return Result<string>.Success(fileUrl);
            }

            return Result<string>.Failure(new Exception("No suitable file URL found."));
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(ex);
        }
    }
}