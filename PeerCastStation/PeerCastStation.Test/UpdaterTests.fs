module UpdaterTests

open Xunit
open System
open PeerCastStation.UI
open TestCommon

let enclosuresAll = [|
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-windows-x86.zip",
        InstallerType.Archive,
        InstallerPlatform.Unknown)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-windows-x86.exe",
        InstallerType.Installer,
        InstallerPlatform.Unknown)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-windows-x86.msi",
        InstallerType.ServiceInstaller,
        InstallerPlatform.Unknown)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-any.zip",
        InstallerType.Archive,
        InstallerPlatform.Any)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-windows-x64.zip",
        InstallerType.Archive,
        InstallerPlatform.WindowsX64)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-windows-x64.exe",
        InstallerType.Installer,
        InstallerPlatform.WindowsX64)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-windows-x64.msi",
        InstallerType.ServiceInstaller,
        InstallerPlatform.WindowsX64)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-windows-x86.zip",
        InstallerType.Archive,
        InstallerPlatform.WindowsX86)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-windows-x86.zip",
        InstallerType.Archive,
        InstallerPlatform.WindowsX86)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-windows-x86.exe",
        InstallerType.Installer,
        InstallerPlatform.WindowsX86)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-windows-x86.msi",
        InstallerType.ServiceInstaller,
        InstallerPlatform.WindowsX86)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-linux-x64.zip",
        InstallerType.Archive,
        InstallerPlatform.LinuxX64)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-linux-musl-x64.zip",
        InstallerType.Archive,
        InstallerPlatform.LinuxMuslX64)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-linux-arm.zip",
        InstallerType.Archive,
        InstallerPlatform.LinuxArm)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-linux-musl-arm.zip",
        InstallerType.Archive,
        InstallerPlatform.LinuxMuslArm)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-linux-arm64.zip",
        InstallerType.Archive,
        InstallerPlatform.LinuxArm64)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-linux-musl-arm64.zip",
        InstallerType.Archive,
        InstallerPlatform.LinuxMuslArm64)
|]

let enclosuresMin = [|
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-any.zip",
        InstallerType.Archive,
        InstallerPlatform.Unknown)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-any.exe",
        InstallerType.Installer,
        InstallerPlatform.Unknown)
    VersionEnclosure(
        "hoge",
        12345L,
        "application/octet-stream",
        Uri "http://example.com/files/hoge-any.msi",
        InstallerType.ServiceInstaller,
        InstallerPlatform.Unknown)
|]

[<Fact>]
let ``インストールタイプとプラットフォームから適切なファイルを選択できる`` () =
    [
        ((InstallerType.Archive, InstallerPlatform.Unknown), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.Any), (InstallerType.Archive, InstallerPlatform.Any))
        ((InstallerType.Archive, InstallerPlatform.WindowsX86), (InstallerType.Archive, InstallerPlatform.WindowsX86))
        ((InstallerType.Archive, InstallerPlatform.WindowsX64), (InstallerType.Archive, InstallerPlatform.WindowsX64))
        ((InstallerType.Archive, InstallerPlatform.WindowsArm), (InstallerType.Archive, InstallerPlatform.WindowsX86))
        ((InstallerType.Archive, InstallerPlatform.WindowsArm64), (InstallerType.Archive, InstallerPlatform.WindowsX86))
        ((InstallerType.Installer, InstallerPlatform.WindowsX86), (InstallerType.Installer, InstallerPlatform.WindowsX86))
        ((InstallerType.Installer, InstallerPlatform.WindowsX64), (InstallerType.Installer, InstallerPlatform.WindowsX64))
        ((InstallerType.Installer, InstallerPlatform.WindowsArm), (InstallerType.Installer, InstallerPlatform.WindowsX86))
        ((InstallerType.Installer, InstallerPlatform.WindowsArm64), (InstallerType.Installer, InstallerPlatform.WindowsX86))
        ((InstallerType.ServiceInstaller, InstallerPlatform.WindowsX86), (InstallerType.ServiceInstaller, InstallerPlatform.WindowsX86))
        ((InstallerType.ServiceInstaller, InstallerPlatform.WindowsX64), (InstallerType.ServiceInstaller, InstallerPlatform.WindowsX64))
        ((InstallerType.ServiceInstaller, InstallerPlatform.WindowsArm), (InstallerType.ServiceInstaller, InstallerPlatform.WindowsX86))
        ((InstallerType.ServiceInstaller, InstallerPlatform.WindowsArm64), (InstallerType.ServiceInstaller, InstallerPlatform.WindowsX86))
        ((InstallerType.Archive, InstallerPlatform.LinuxX64), (InstallerType.Archive, InstallerPlatform.LinuxX64))
        ((InstallerType.Archive, InstallerPlatform.LinuxMuslX64), (InstallerType.Archive, InstallerPlatform.LinuxMuslX64))
        ((InstallerType.Archive, InstallerPlatform.LinuxArm), (InstallerType.Archive, InstallerPlatform.LinuxArm))
        ((InstallerType.Archive, InstallerPlatform.LinuxMuslArm), (InstallerType.Archive, InstallerPlatform.LinuxMuslArm))
        ((InstallerType.Archive, InstallerPlatform.LinuxArm64), (InstallerType.Archive, InstallerPlatform.LinuxArm64))
        ((InstallerType.Archive, InstallerPlatform.LinuxMuslArm64), (InstallerType.Archive, InstallerPlatform.LinuxMuslArm64))
        ((InstallerType.Archive, InstallerPlatform.MacX64), (InstallerType.Archive, InstallerPlatform.Any))
        ((InstallerType.Archive, InstallerPlatform.MacArm64), (InstallerType.Archive, InstallerPlatform.Any))
    ]
    |> List.iter (fun ((specifiedType, specifiedPlatform), (expectedType, expectedPlatform)) ->
        let enclosure = Updater.SelectEnclosure(enclosuresAll, specifiedType, specifiedPlatform)
        Assert.Equal(expectedType, enclosure.InstallerType)
        Assert.Equal(expectedPlatform, enclosure.InstallerPlatform)
    )
    [
        ((InstallerType.Archive, InstallerPlatform.Unknown), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.Any), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.WindowsX86), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.WindowsX64), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.WindowsArm), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.WindowsArm64), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Installer, InstallerPlatform.WindowsX86), (InstallerType.Installer, InstallerPlatform.Unknown))
        ((InstallerType.Installer, InstallerPlatform.WindowsX64), (InstallerType.Installer, InstallerPlatform.Unknown))
        ((InstallerType.Installer, InstallerPlatform.WindowsArm), (InstallerType.Installer, InstallerPlatform.Unknown))
        ((InstallerType.Installer, InstallerPlatform.WindowsArm64), (InstallerType.Installer, InstallerPlatform.Unknown))
        ((InstallerType.ServiceInstaller, InstallerPlatform.WindowsX86), (InstallerType.ServiceInstaller, InstallerPlatform.Unknown))
        ((InstallerType.ServiceInstaller, InstallerPlatform.WindowsX64), (InstallerType.ServiceInstaller, InstallerPlatform.Unknown))
        ((InstallerType.ServiceInstaller, InstallerPlatform.WindowsArm), (InstallerType.ServiceInstaller, InstallerPlatform.Unknown))
        ((InstallerType.ServiceInstaller, InstallerPlatform.WindowsArm64), (InstallerType.ServiceInstaller, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.LinuxX64), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.LinuxMuslX64), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.LinuxArm), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.LinuxMuslArm), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.LinuxArm64), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.LinuxMuslArm64), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.MacX64), (InstallerType.Archive, InstallerPlatform.Unknown))
        ((InstallerType.Archive, InstallerPlatform.MacArm64), (InstallerType.Archive, InstallerPlatform.Unknown))
    ]
    |> List.iter (fun ((specifiedType, specifiedPlatform), (expectedType, expectedPlatform)) ->
        let enclosure = Updater.SelectEnclosure(enclosuresMin, specifiedType, specifiedPlatform)
        Assert.Equal(expectedType, enclosure.InstallerType)
        Assert.Equal(expectedPlatform, enclosure.InstallerPlatform)
    )

[<Fact>]
let ``プラットフォーム指定の文字列をパースできる`` () =
    [
      ("any", InstallerPlatform.Any)
      ("win-x86", InstallerPlatform.WindowsX86)
      ("win-x64", InstallerPlatform.WindowsX64)
      ("win-arm", InstallerPlatform.WindowsArm)
      ("win-arm64", InstallerPlatform.WindowsArm64)
      ("linux-x64", InstallerPlatform.LinuxX64)
      ("linux-musl-x64", InstallerPlatform.LinuxMuslX64)
      ("linux-arm", InstallerPlatform.LinuxArm)
      ("linux-musl-arm", InstallerPlatform.LinuxMuslArm)
      ("linux-arm64", InstallerPlatform.LinuxArm64)
      ("linux-musl-arm64", InstallerPlatform.LinuxMuslArm64)
      ("osx-x64", InstallerPlatform.MacX64)
      ("osx-arm64", InstallerPlatform.MacArm64)
      ("unknown", InstallerPlatform.Unknown)
      ("ANY", InstallerPlatform.Any)
      ("WIN-X86", InstallerPlatform.WindowsX86)
      ("WIN-X64", InstallerPlatform.WindowsX64)
      ("WIN-ARM", InstallerPlatform.WindowsArm)
      ("WIN-ARM64", InstallerPlatform.WindowsArm64)
      ("LINUX-X64", InstallerPlatform.LinuxX64)
      ("LINUX-MUSL-X64", InstallerPlatform.LinuxMuslX64)
      ("LINUX-ARM", InstallerPlatform.LinuxArm)
      ("LINUX-MUSL-ARM", InstallerPlatform.LinuxMuslArm)
      ("LINUX-ARM64", InstallerPlatform.LinuxArm64)
      ("LINUX-MUSL-ARM64", InstallerPlatform.LinuxMuslArm64)
      ("OSX-X64", InstallerPlatform.MacX64)
      ("OSX-ARM64", InstallerPlatform.MacArm64)
      ("UNKNOWN", InstallerPlatform.Unknown)
      ("hoge", InstallerPlatform.Unknown)
    ]
    |> List.iter (fun (specifiedStr, expectedPlatform) ->
        Assert.Equal(expectedPlatform, Updater.ParsePlatformString(specifiedStr))
    )

let appCast = """<?xml version="1.0" encoding="utf-8"?>
<rss version="2.0" xmlns:dc="http://purl.org/dc/elements/1.1/">
  <channel>
    <title>PeerCastStation更新情報</title>
    <link>http://www.pecastation.org/</link>
    <description>PeerCastStationの更新情報です</description>
    <language>ja</language>
    <item>
      <title>Version 2.5.1.209 (Test Only)</title>
      <description>
          <h1>Version 2.5.1.209 (Test Only)</h1>
          <p>テスト用です。見なかったことにしてください。</p>
      </description>
      <pubDate>Mon, 28 May 2018 01:00:00 +0900</pubDate>
      <link>http://example.com/files/hoge-windows-x86.zip</link>
      <enclosure installer-type="archive" install-command="PeerCastStation update" url="http://example.com/files/hoge-windows-x86.zip" length="123456" type="application/octet-stream" />
      <enclosure installer-type="installer" url="http://example.com/files/hoge-windows-x86.exe" length="2341639" type="application/octet-stream" />
      <enclosure installer-type="serviceinstaller" url="http://example.com/files/hoge-windows-x86.msi" length="2024226" type="application/octet-stream" />
      <enclosure installer-type="archive" installer-platform="any" install-command="PeerCastStation update" url="http://example.com/files/hoge-any.zip" length="123456" type="application/octet-stream" />
      <enclosure installer-type="archive" installer-platform="win-x86" install-command="PeerCastStation update" url="http://example.com/files/hoge-windows-x86.zip" length="123456" type="application/octet-stream" />
      <enclosure installer-type="installer" installer-platform="win-x86" url="http://example.com/files/hoge-windows-x86.exe" length="2341639" type="application/octet-stream" />
      <enclosure installer-type="serviceinstaller" installer-platform="win-x86" url="http://example.com/files/hoge-windows-x86.msi" length="2024226" type="application/octet-stream" />
      <enclosure installer-type="archive" installer-platform="win-x64" install-command="PeerCastStation update" url="http://example.com/files/hoge-windows-x64.zip" length="123456" type="application/octet-stream" />
      <enclosure installer-type="installer" installer-platform="win-x64" url="http://example.com/files/hoge-windows-x64.exe" length="2341639" type="application/octet-stream" />
      <enclosure installer-type="serviceinstaller" installer-platform="win-x64" url="http://example.com/files/hoge-windows-x64.msi" length="2024226" type="application/octet-stream" />
      <enclosure installer-type="archive" installer-platform="linux-x64" install-command="PeerCastStation update" url="http://example.com/files/hoge-linux-x64.zip" length="123456" type="application/octet-stream" />
      <enclosure installer-type="archive" installer-platform="linux-arm" install-command="PeerCastStation update" url="http://example.com/files/hoge-linux-arm.zip" length="123456" type="application/octet-stream" />
      <enclosure installer-type="archive" installer-platform="linux-arm64" install-command="PeerCastStation update" url="http://example.com/files/hoge-linux-arm64.zip" length="123456" type="application/octet-stream" />
      <enclosure installer-type="archive" installer-platform="linux-musl-x64" install-command="PeerCastStation update" url="http://example.com/files/hoge-linux-musl-x64.zip" length="123456" type="application/octet-stream" />
      <enclosure installer-type="archive" installer-platform="linux-musl-arm" install-command="PeerCastStation update" url="http://example.com/files/hoge-linux-musl-arm.zip" length="123456" type="application/octet-stream" />
      <enclosure installer-type="archive" installer-platform="linux-musl-arm64" install-command="PeerCastStation update" url="http://example.com/files/hoge-linux-musl-arm64.zip" length="123456" type="application/octet-stream" />
    </item>
  </channel>
</rss>
"""

[<Fact>]
let ``AppCastでプラットフォーム毎のEnclosureが取得できる`` () =
    let reader = AppCastReader()
    let versionDesciptions = reader.ParseAppCastString(appCast)
    Assert.Equal(1, Seq.length versionDesciptions)
    let ver = Seq.head versionDesciptions
    [
        (InstallerType.Archive, InstallerPlatform.Unknown, true)
        (InstallerType.Archive, InstallerPlatform.Any, true)
        (InstallerType.Archive, InstallerPlatform.WindowsX86, true)
        (InstallerType.Archive, InstallerPlatform.WindowsX64, true)
        (InstallerType.Archive, InstallerPlatform.WindowsArm, false)
        (InstallerType.Archive, InstallerPlatform.WindowsArm64, false)
        (InstallerType.Installer, InstallerPlatform.WindowsX86, true)
        (InstallerType.Installer, InstallerPlatform.WindowsX64, true)
        (InstallerType.Installer, InstallerPlatform.WindowsArm, false)
        (InstallerType.Installer, InstallerPlatform.WindowsArm64, false)
        (InstallerType.ServiceInstaller, InstallerPlatform.WindowsX86, true)
        (InstallerType.ServiceInstaller, InstallerPlatform.WindowsX64, true)
        (InstallerType.ServiceInstaller, InstallerPlatform.WindowsArm, false)
        (InstallerType.ServiceInstaller, InstallerPlatform.WindowsArm64, false)
        (InstallerType.Archive, InstallerPlatform.LinuxX64, true)
        (InstallerType.Archive, InstallerPlatform.LinuxMuslX64, true)
        (InstallerType.Archive, InstallerPlatform.LinuxArm, true)
        (InstallerType.Archive, InstallerPlatform.LinuxMuslArm, true)
        (InstallerType.Archive, InstallerPlatform.LinuxArm64, true)
        (InstallerType.Archive, InstallerPlatform.LinuxMuslArm64, true)
        (InstallerType.Archive, InstallerPlatform.MacX64, false)
        (InstallerType.Archive, InstallerPlatform.MacArm64, false)
    ]
    |> List.iter (fun (itype, iplatform, exists) ->
        Assert.Equal(exists,
            ver.Enclosures
            |> Seq.exists (fun enc -> enc.InstallerPlatform = iplatform && enc.InstallerType = itype)
        )
    )

[<Fact>]
let ``AppCastからインストールコマンドが取得できる`` () =
    let reader = AppCastReader()
    let versionDesciptions = reader.ParseAppCastString(appCast)
    Assert.Equal(1, Seq.length versionDesciptions)
    let ver = Seq.head versionDesciptions
    ver.Enclosures
    |> Seq.filter (fun enc -> enc.InstallerType = InstallerType.Archive)
    |> Seq.iter (fun enc -> Assert.Equal("PeerCastStation update", enc.InstallCommand))

[<Fact>]
let ``FindDotNetでdotnetコマンドを取得できる`` () =
    let dotnet = Updater.FindDotNet()
    Assert.NotEqual<string>("dotnet", dotnet)
    Assert.Contains("dotnet", dotnet)

let archiveFixture (sourceDir:string) targetDir =
    let targetFile = System.IO.Path.Join(targetDir, System.IO.Path.GetFileName(sourceDir) + ".zip")
    System.IO.Compression.ZipFile.CreateFromDirectory(sourceDir, targetFile, System.IO.Compression.CompressionLevel.Fastest, false)
    targetFile

let readTextFile filename =
    let rec readTextFileInternal retry =
        try
            IO.File.ReadAllText(filename)
        with
        | :? System.IO.FileNotFoundException ->
            if retry>0 then
                System.Threading.Thread.Sleep(100)
                readTextFileInternal (retry - 1)
            else
                reraise()
    readTextFileInternal 5
    
type TestApplication () =
    inherit PeerCastStation.Core.PeerCastApplication()
    let mutable onCleanup = fun () -> ()
    let settings = PeerCastStation.Core.PecaSettings("settings.xml")
    let peerCast = new PeerCastStation.Core.PeerCast()
    override this.Settings = settings
    override this.Plugins = Seq.empty
    override this.PeerCast = peerCast
    override this.BasePath = "."
    override this.Args = [||]
    override this.Stop(exitCode, cleanupHandler) =
        onCleanup <- fun () -> cleanupHandler.Invoke()
    override this.SaveSettings() = ()

    interface IDisposable with
        member this.Dispose() =
            peerCast.Dispose()
            onCleanup ()

[<Fact>]
let ``アーカイブ内のインストールコマンドがアプリ終了時に実行される`` () =
    use tempDir = new TempDirectory()
    let zipFile = archiveFixture "fixtures/UpdateArchive" tempDir.FullName
    let downloadResult = Updater.DownloadResult(
        zipFile,
        VersionDescription(
            DateTime.Now,
            Uri "http://example.com/test.zip",
            "test",
            "test",
            []
        ),
        VersionEnclosure(
            "test",
            0L,
            "application/octet-stream",
            Uri "http://example.com/test.zip",
            InstallerType.Archive,
            InstallerPlatform.Unknown,
            "Updater")
    )
    let doInstall () =
        use app = new TestApplication()
        let installResult = Updater.Install(downloadResult, tempDir.FullName)
        Assert.True(installResult)
    doInstall()
    let text = IO.Path.Combine(tempDir.FullName, "UpdaterTest.txt") |> readTextFile
    Assert.Matches(sprintf @"UpdaterTest update (\S+) %s" (System.Text.RegularExpressions.Regex.Escape(tempDir.FullName)), text.Trim())

