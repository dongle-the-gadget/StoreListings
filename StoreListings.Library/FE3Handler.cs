using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using StoreListings.Library.Internal;

namespace StoreListings.Library;

public static partial class FE3Handler
{
    public class Cookie
    {
        public required string CookieData
        {
            get; set;
        }

        public required string Expiration
        {
            get; set;
        }
    }

    private class InternalSyncUpdatesResponse
    {
        public required Dictionary<string, string> NewUpdatesInfo
        {
            get; set;
        }

        public required Dictionary<string, string> ExtendedUpdatesInfo
        {
            get; set;
        }
    }

    public class SyncUpdatesResponse
    {
        public class Update
        {
            public class Platform
            {
                public required Version MinVersion
                {
                    get; set;
                }

                public required DeviceFamily Family
                {
                    get; set;
                }
            }

            public required string FileName
            {
                get; set;
            }

            public required string UpdateID
            {
                get; set;
            }

            public required string RevisionNumber
            {
                get; set;
            }

            public required string Digest
            {
                get; set;
            }

            public required string PackageIdentityName
            {
                get; set;
            }

            public required Version Version
            {
                get; set;
            }

            public required bool IsFramework
            {
                get; set;
            }

            public required IEnumerable<Platform> TargetPlatforms
            {
                get; set;
            }
        }

        public class Bundle
        {
            /// <summary>
            /// The GUID (UpdateID) of this bundle update.
            /// </summary>
            public required string UpdateID
            {
                get; set;
            }

            /// <summary>
            /// GUIDs listed under BundledUpdates/AtLeastOne — the bundle's own
            /// per-architecture child packages (the OS installs one of them).
            /// </summary>
            public required List<string> PackageIds
            {
                get; set;
            }

            /// <summary>
            /// GUIDs that are direct children of BundledUpdates (outside AtLeastOne) —
            /// the mandatory framework dependency bundles for this binary version.
            /// </summary>
            public required List<string> DependencyIds
            {
                get; set;
            }
        }

        public required IEnumerable<Update> Updates
        {
            get; set;
        }

        public required IEnumerable<Bundle> Bundles
        {
            get; set;
        }

        public required Cookie NewCookie
        {
            get; set;
        }

        private Dictionary<string, Update>? _leafByUpdateId;
        private Dictionary<string, Bundle>? _bundleByUpdateId;

        /// <summary>
        /// Resolves the exact framework dependencies Microsoft bundled with this specific
        /// binary by walking the FE3 bundle tree (BundledUpdates).
        /// </summary>
        public IReadOnlyList<Update> ResolveDependencies(Update mainPackage)
        {
            _leafByUpdateId ??= IndexByUpdateId(Updates, u => u.UpdateID);
            _bundleByUpdateId ??= IndexByUpdateId(Bundles, b => b.UpdateID);

            // The version bundle is the one whose AtLeastOne lists this app package.
            Bundle? versionBundle = Bundles.FirstOrDefault(b =>
                b.PackageIds.Contains(mainPackage.UpdateID, StringComparer.OrdinalIgnoreCase)
            );
            if (versionBundle is null)
                return [];

            List<Update> result = new();
            HashSet<string> visitedBundles = new(StringComparer.OrdinalIgnoreCase);
            HashSet<string> addedLeaves = new(StringComparer.OrdinalIgnoreCase);
            Queue<string> pending = new(versionBundle.DependencyIds);

            while (pending.Count > 0)
            {
                string depId = pending.Dequeue();
                if (_bundleByUpdateId.TryGetValue(depId, out Bundle? depBundle))
                {
                    if (!visitedBundles.Add(depId))
                        continue;
                    // A framework dependency points at a (neutral) bundle whose
                    // AtLeastOne lists that framework's per-architecture leaves.
                    foreach (string leafId in depBundle.PackageIds)
                    {
                        if (
                            _leafByUpdateId.TryGetValue(leafId, out Update? leaf)
                            && addedLeaves.Add(leaf.UpdateID)
                        )
                            result.Add(leaf);
                    }
                    // Defensive: follow any transitive dependency bundles.
                    foreach (string nested in depBundle.DependencyIds)
                        pending.Enqueue(nested);
                }
                else if (
                    _leafByUpdateId.TryGetValue(depId, out Update? directLeaf)
                    && addedLeaves.Add(directLeaf.UpdateID)
                )
                {
                    // A dependency that points straight at a leaf package.
                    result.Add(directLeaf);
                }
            }

            return result;
        }

        private static Dictionary<string, T> IndexByUpdateId<T>(
            IEnumerable<T> items,
            Func<T, string> updateIdSelector
        )
        {
            Dictionary<string, T> map = new(StringComparer.OrdinalIgnoreCase);
            foreach (T item in items)
                map.TryAdd(updateIdSelector(item), item);
            return map;
        }
    }

    private const string WuNamespace =
        "http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService";
    private const string SoapNamespace = "http://www.w3.org/2003/05/soap-envelope";

    private static XName Wu(string name) => XName.Get(name, WuNamespace);

    private static XName Soap(string name) => XName.Get(name, SoapNamespace);

    public static DeviceFamily ConvertFE3PlatformToDeviceFamily(long platform) =>
        platform switch
        {
            0 => DeviceFamily.Universal,
            3 => DeviceFamily.Desktop,
            4 => DeviceFamily.Mobile,
            5 => DeviceFamily.Xbox,
            6 => DeviceFamily.Team,
            10 => DeviceFamily.Holographic,
            16 => DeviceFamily.Core,
            _ => DeviceFamily.Unknown,
        };

    public static async Task<Result<Cookie>> GetCookieAsync(
        CancellationToken cancellationToken = default
    )
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
                                <Created>{DateTime.UtcNow.ToString(
                    "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"
                )}</Created>
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
                            <currentTime>{DateTime.UtcNow.ToString(
                    "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"
                )}</currentTime>
                            <protocolVersion>2.50</protocolVersion>
                        </GetCookie>
                    </Body>
                </Envelope>
                """;

            HttpClient client = Helpers.GetFE3StoreHttpClient();
            using HttpResponseMessage response = await client.PostAsync(
                "https://fe3cr.delivery.mp.microsoft.com/ClientWebService/client.asmx",
                new StringContent(cookie, Encoding.UTF8, "application/soap+xml"),
                cancellationToken
            );
            response.EnsureSuccessStatusCode();
            XElement doc = await XElement.LoadAsync(
                await response.Content.ReadAsStreamAsync(),
                LoadOptions.None,
                cancellationToken
            );
            XElement body = doc.Element(Soap("Body"))!;
            XElement getCookieResponse = body.Element(Wu("GetCookieResponse"))!;
            XElement getCookieResult = getCookieResponse.Element(Wu("GetCookieResult"))!;

            XElement cookieData = getCookieResult.Element(Wu("EncryptedData"))!;
            XElement expiration = getCookieResult.Element(Wu("Expiration"))!;

            return Result<Cookie>.Success(
                new Cookie { CookieData = cookieData.Value, Expiration = expiration.Value }
            );
        }
        catch (Exception ex)
        {
            return Result<Cookie>.Failure(ex);
        }
    }

    public static async Task<Result<SyncUpdatesResponse>> SyncUpdatesAsync(
        Cookie cookie,
        string WuCategoryId,
        Lang lang,
        Market market,
        string currentBranch,
        string flightRing,
        string flightingBranchName,
        Version OSVersion,
        DeviceFamily deviceFamily,
        CancellationToken cancellationToken = default,
        FE3OSArch osArchitecture = FE3OSArch.AMD64
    )
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
                content = GenerateSyncUpdatesPayload(
                    currentCookie,
                    WuCategoryId,
                    lang,
                    market,
                    currentBranch,
                    flightRing,
                    flightingBranchName,
                    OSVersion,
                    deviceFamily,
                    FoundUpdateIDs,
                    FoundUpdateIDs,
                    osArchitecture
                );

                using HttpResponseMessage response = await client.PostAsync(
                    "https://fe3cr.delivery.mp.microsoft.com/ClientWebService/client.asmx",
                    new StringContent(content, Encoding.UTF8, "application/soap+xml"),
                    cancellationToken
                );
                response.EnsureSuccessStatusCode();
                doc = await XElement.LoadAsync(
                    await response.Content.ReadAsStreamAsync(),
                    LoadOptions.None,
                    cancellationToken
                );

                XElement result = doc.Element(Soap("Body"))!
                    .Element(Wu("SyncUpdatesResponse"))!
                    .Element(Wu("SyncUpdatesResult"))!;

                XElement newCookieXml = result.Element(Wu("NewCookie"))!;

                currentCookie = new Cookie()
                {
                    CookieData = newCookieXml.Element(Wu("EncryptedData"))!.Value,
                    Expiration = newCookieXml.Element(Wu("Expiration"))!.Value,
                };

                XElement? extendedUpdatesInfo = result.Element(Wu("ExtendedUpdateInfo"));

                int extendedUpdatesNum;

                if (
                    extendedUpdatesInfo is not null
                    && extendedUpdatesInfo.Element(Wu("Updates"))!.Elements(Wu("Update"))
                        is { } updates
                    && (extendedUpdatesNum = updates.Count()) > 0
                )
                {
                    IEnumerable<XElement> newUpdates = result
                        .Element(Wu("NewUpdates"))!
                        .Elements(Wu("UpdateInfo"));
                    Dictionary<string, string> NewUpdateInfo = new(newUpdates.Count());
                    Dictionary<string, string> ExtendedUpdatesInfo = new(extendedUpdatesNum);
                    foreach (XElement update in updates)
                    {
                        string id = update.Element(Wu("ID"))!.Value;
                        FoundUpdateIDs.Add(id);
                        ExtendedUpdatesInfo.Add(id, update.Element(Wu("Xml"))!.Value);
                    }

                    foreach (XElement newUpdate in newUpdates)
                    {
                        string id = newUpdate.Element(Wu("ID"))!.Value;
                        NewUpdateInfo.Add(id, newUpdate.Element(Wu("Xml"))!.Value);
                    }

                    responses.Add(
                        new InternalSyncUpdatesResponse()
                        {
                            ExtendedUpdatesInfo = ExtendedUpdatesInfo,
                            NewUpdatesInfo = NewUpdateInfo,
                        }
                    );
                }
                else
                {
                    break;
                }
            }

            List<SyncUpdatesResponse.Update> updateResponses = new();
            List<SyncUpdatesResponse.Bundle> bundleResponses = new();

            foreach (InternalSyncUpdatesResponse response in responses)
            {
                foreach ((string id, string coreFragment) in response.NewUpdatesInfo)
                {
                    // Bundle parent: holds the dependency tree in <BundledUpdates>, no payload.
                    if (TryParseBundle(coreFragment) is { } bundle)
                    {
                        bundleResponses.Add(bundle);
                        continue;
                    }

                    // Leaf package: needs the extended fragment's <Files> and a SecuredFragment URL.
                    if (
                        !response.ExtendedUpdatesInfo.TryGetValue(id, out string? extendedFragment)
                        || !extendedFragment.Contains("<Files")
                        || !coreFragment.Contains("SecuredFragment")
                    )
                        continue;
                    string appendedUpdateInfoXml = $"<Xml>{extendedFragment}{coreFragment}</Xml>";
                    doc = XElement.Parse(appendedUpdateInfoXml);

                    XElement updateIdentity = doc.Element("UpdateIdentity")!;
                    string updateId = updateIdentity.Attribute("UpdateID")!.Value;
                    string revisionNumber = updateIdentity.Attribute("RevisionNumber")!.Value;

                    XElement extendedProperties = doc.Element("ExtendedProperties")!;
                    string packageIdentityName = extendedProperties
                        .Attribute("PackageIdentityName")!
                        .Value;
                    bool isFramework = (bool)extendedProperties.Attribute("IsAppxFramework")!;

                    List<SyncUpdatesResponse.Update.Platform> platforms;

                    using (
                        JsonDocument jsonApplicabilityDoc = JsonDocument.Parse(
                            doc.Element("ApplicabilityRules")!
                                .Element("Metadata")!
                                .Element("AppxPackageMetadata")!
                                .Element("AppxMetadata")!
                                .Element("ApplicabilityBlob")!
                                .Value
                        )
                    )
                    {
                        if (
                            !jsonApplicabilityDoc.RootElement.TryGetProperty(
                                "content.targetPlatforms",
                                out JsonElement targetPlatforms
                            )
                        )
                        {
                            // for old style use request platform
                            platforms =
                            [
                                new SyncUpdatesResponse.Update.Platform
                                {
                                    Family = deviceFamily,
                                    MinVersion = OSVersion,
                                },
                            ];
                        }
                        else
                        {
                            int numPlatforms = targetPlatforms.GetArrayLength();
                            platforms = new(numPlatforms);
                            for (int i = 0; i < numPlatforms; i++)
                            {
                                JsonElement targetPlatform = targetPlatforms[i];
                                SyncUpdatesResponse.Update.Platform platform = new()
                                {
                                    Family = ConvertFE3PlatformToDeviceFamily(
                                        targetPlatform.GetProperty("platform.target").GetInt64()
                                    ),
                                    MinVersion = Version.FromWindowsRepresentation(
                                        targetPlatform
                                            .GetProperty("platform.minVersion")
                                            .GetUInt64()
                                    ),
                                };
                                platforms.Add(platform);
                            }
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

                        updateResponses.Add(
                            new SyncUpdatesResponse.Update()
                            {
                                PackageIdentityName = packageIdentityName,
                                FileName = realName,
                                UpdateID = updateId,
                                Digest = digest,
                                RevisionNumber = revisionNumber,
                                Version = Version.Parse(
                                    identifier.AsSpan()[firstIndex..][..secondIndex],
                                    null
                                ),
                                TargetPlatforms = platforms,
                                IsFramework = isFramework,
                            }
                        );
                    }
                }
            }
            return Result<SyncUpdatesResponse>.Success(
                new SyncUpdatesResponse()
                {
                    NewCookie = currentCookie,
                    Updates = updateResponses,
                    Bundles = bundleResponses,
                }
            );
        }
        catch (Exception ex)
        {
            return Result<SyncUpdatesResponse>.Failure(ex);
        }
    }

    /// <summary>
    /// Parses a bundle parent's Core/published fragment into its BundledUpdates relationship,
    /// or returns null for leaf packages.
    /// </summary>
    private static SyncUpdatesResponse.Bundle? TryParseBundle(string coreFragment)
    {
        if (!coreFragment.Contains("<BundledUpdates"))
            return null;

        XElement doc = XElement.Parse($"<Xml>{coreFragment}</Xml>");
        XElement? updateIdentity = doc.Element("UpdateIdentity");
        XElement? bundledUpdates = doc.Element("Relationships")?.Element("BundledUpdates");
        if (updateIdentity is null || bundledUpdates is null)
            return null;

        static List<string> UpdateIds(IEnumerable<XElement> elements) =>
            elements.Select(e => e.Attribute("UpdateID")!.Value).ToList();

        return new SyncUpdatesResponse.Bundle
        {
            UpdateID = updateIdentity.Attribute("UpdateID")!.Value,
            // AtLeastOne children = this bundle's own per-architecture packages.
            PackageIds = UpdateIds(bundledUpdates.Elements("AtLeastOne").Elements("UpdateIdentity")),
            // Direct UpdateIdentity children (outside AtLeastOne) = dependency bundles.
            DependencyIds = UpdateIds(bundledUpdates.Elements("UpdateIdentity")),
        };
    }

    private static string GenerateSyncUpdatesPayload(
        Cookie cookie,
        string WuCategoryId,
        Lang lang,
        Market market,
        string currentBranch,
        string flightRing,
        string flightingBranchName,
        Version OSVersion,
        DeviceFamily deviceFamily,
        IEnumerable<string> additionalInstalledNonLeafUpdateIDs,
        IEnumerable<string> additionalOtherCachedUpdateIDs,
        FE3OSArch osArch = FE3OSArch.AMD64
    )
    {
        int flightEnabled = flightRing == "Retail" ? 0 : 1;
        string installType = deviceFamily switch
        {
            DeviceFamily.IoTUAP => "IoTUAP",
            DeviceFamily.Iot => "IoTUAP", // Not too sure about this one
            DeviceFamily.Server => "Server",
            DeviceFamily.Holographic => "FactoryOS",
            DeviceFamily.Core => "FactoryOS",
            _ => "Client",
        };

        string cached = string.Join(
            Environment.NewLine,
            additionalOtherCachedUpdateIDs.Select(x => $"<int>{x}</int>")
        );
        string nonleaf = string.Join(
            Environment.NewLine,
            additionalInstalledNonLeafUpdateIDs.Select(x => $"<int>{x}</int>")
        );

        return $"""
            <s:Envelope xmlns:a="http://www.w3.org/2005/08/addressing" xmlns:s="http://www.w3.org/2003/05/soap-envelope">
              <s:Header>
                <a:Action s:mustUnderstand="1">http://www.microsoft.com/SoftwareDistribution/Server/ClientWebService/SyncUpdates</a:Action>
                <a:MessageID>urn:uuid:{Guid.NewGuid()}</a:MessageID>
                <a:To s:mustUnderstand="1">https://fe3.delivery.mp.microsoft.com/ClientWebService/client.asmx</a:To>
                <o:Security s:mustUnderstand="1" xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
                  <Timestamp xmlns="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd">
                    <Created>{DateTime.UtcNow.ToString(
                "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"
            )}</Created>
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
                      <DeviceAttributes>BranchReadinessLevel=CB;CurrentBranch={currentBranch};OEMModel=Virtual Machine;FlightRing={flightRing};AttrDataVer=21;SystemManufacturer=Microsoft Corporation;InstallLanguage={lang}-{market};OSUILocale={lang}-{market};InstallationType={installType};FlightingBranchName={flightingBranchName};FirmwareVersion=Hyper-V UEFI Release v2.5;SystemProductName=Virtual Machine;OSSkuId=48;FlightContent=Mainline;App=WU_STORE;OEMName_Uncleaned=Microsoft Corporation;AppVer=0.0.0.0;OSArchitecture={osArch};SystemSKU=None;UpdateManagementGroup=2;IsFlightingEnabled={flightEnabled};IsDeviceRetailDemo=0;TelemetryLevel=3;OSVersion={OSVersion};DeviceFamily=Windows.{deviceFamily};</DeviceAttributes>
                      <CallerAttributes>Interactive=1;IsSeeker=0;</CallerAttributes>
                      <Products />
                    </ProductsParameters>
                  </parameters>
                </SyncUpdates>
              </s:Body>
            </s:Envelope>
            """;
    }

    public static async Task<Result<PackageDownloadInfo>> GetPackageDownloadInfo(
        Cookie cookie,
        string updateID,
        string revisionNumber,
        string packageDigest,
        Lang lang,
        Market market,
        string currentBranch,
        string flightRing,
        string flightingBranchName,
        Version OSVersion,
        DeviceFamily deviceFamily,
        CancellationToken cancellationToken = default,
        FE3OSArch osArch = FE3OSArch.AMD64
    )
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
                _ => "Client",
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
                                <Created>{DateTime.UtcNow.ToString(
                    "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"
                )}</Created>
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
                            <deviceAttributes>BranchReadinessLevel=CB;CurrentBranch={currentBranch};OEMModel=Virtual Machine;FlightRing={flightRing};AttrDataVer=21;SystemManufacturer=Microsoft Corporation;InstallLanguage={lang}-{market};OSUILocale={lang}-{market};InstallationType={installType};FlightingBranchName={flightingBranchName};FirmwareVersion=Hyper-V UEFI Release v2.5;SystemProductName=Virtual Machine;OSSkuId=48;FlightContent=Mainline;App=WU_STORE;OEMName_Uncleaned=Microsoft Corporation;AppVer=0.0.0.0;OSArchitecture={osArch};SystemSKU=None;UpdateManagementGroup=2;IsFlightingEnabled={flightEnabled};IsDeviceRetailDemo=0;TelemetryLevel=3;OSVersion={OSVersion};DeviceFamily=Windows.{deviceFamily};</deviceAttributes>
                        </GetExtendedUpdateInfo2>
                    </s:Body>
                </s:Envelope>
                """;

            HttpClient client = Helpers.GetFE3StoreHttpClient();
            using HttpResponseMessage response = await client.PostAsync(
                "https://fe3cr.delivery.mp.microsoft.com/ClientWebService/client.asmx/secured",
                new StringContent(content, Encoding.UTF8, "application/soap+xml"),
                cancellationToken
            );
            response.EnsureSuccessStatusCode();
            XElement doc = await XElement.LoadAsync(
                await response.Content.ReadAsStreamAsync(),
                LoadOptions.None,
                cancellationToken
            );
            XElement body = doc.Element(Soap("Body"))!;

            XElement getExtendedUpdateInfo2Response = body.Element(
                Wu("GetExtendedUpdateInfo2Response")
            )!;
            XElement getExtendedUpdateInfo2Result = getExtendedUpdateInfo2Response.Element(
                Wu("GetExtendedUpdateInfo2Result")
            )!;
            XElement fileLocations = getExtendedUpdateInfo2Result.Element(Wu("FileLocations"))!;
            IEnumerable<XElement> listOfLocations = fileLocations.Elements(Wu("FileLocation"));

            string? packageUrl = null;
            string? blockmapUrl = null;
            string? blockmapDigest = null;

            foreach (XElement fileLocation in listOfLocations)
            {
                string fileDigest = fileLocation.Element(Wu("FileDigest"))!.Value;
                string fileUrl = fileLocation.Element(Wu("Url"))!.Value;

                if (fileDigest == packageDigest)
                    packageUrl = fileUrl;
                else
                {
                    blockmapDigest = fileDigest;
                    blockmapUrl = fileUrl;
                }
            }

            if (packageUrl is null)
                return Result<PackageDownloadInfo>.Failure(
                    new Exception("No suitable package URL found.")
                );

            var pkg = new DownloadResource(packageUrl, packageDigest);
            var blockmap =
                blockmapUrl is null || blockmapDigest is null
                    ? null
                    : new DownloadResource(blockmapUrl, blockmapDigest);

            return Result<PackageDownloadInfo>.Success(new PackageDownloadInfo(pkg, blockmap));
        }
        catch (Exception ex)
        {
            return Result<PackageDownloadInfo>.Failure(ex);
        }
    }
}
