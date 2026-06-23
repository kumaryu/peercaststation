using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// アセンブリに関する一般情報は以下の属性セットをとおして制御されます。
// アセンブリに関連付けられている情報を変更するには、
// これらの属性値を変更してください。

// テストプロジェクト(同一 snk で署名)から internal の VInt/Element 等へアクセスするための friend 宣言。
// 署名アセンブリ間なので full public key の指定が必須。
[assembly: InternalsVisibleTo("PeerCastStation.Test, PublicKey=00240000048000009400000006020000002400005253413100040000010001003b4def7d493cd15e2de42ec2a7edc4f90390af0e8c535e8494b2b4b99e6b95b0c1a6176ee25750801d0698c07dd70f6e131b46e1196e256d5c36d8dd510f73bb5efa555d75bd23d8a93f5f7dbf272876789a3c5f83b8e01914f3e5926b79b61c385a93ef654be66d4ee5f3d485f0d3d94ceecdd96cafc377f7dbf0995b9a5dba")]
// MatroskaContentBufferから MatroskaInitBuilder/MatroskaWriter/Elements 等の internal へアクセスするための friend 宣言。
// FLV は同一 PeerCastStation.snk で署名されるため Test と同じ full public key を使う。
[assembly: InternalsVisibleTo("PeerCastStation.FLV, PublicKey=00240000048000009400000006020000002400005253413100040000010001003b4def7d493cd15e2de42ec2a7edc4f90390af0e8c535e8494b2b4b99e6b95b0c1a6176ee25750801d0698c07dd70f6e131b46e1196e256d5c36d8dd510f73bb5efa555d75bd23d8a93f5f7dbf272876789a3c5f83b8e01914f3e5926b79b61c385a93ef654be66d4ee5f3d485f0d3d94ceecdd96cafc377f7dbf0995b9a5dba")]

[assembly: AssemblyTitle("PeerCastStation.MKV")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("PeerCastStation.MKV")]
[assembly: AssemblyCopyright("Copyright ©  2012")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// ComVisible を false に設定すると、その型はこのアセンブリ内で COM コンポーネントから 
// 参照不可能になります。COM からこのアセンブリ内の型にアクセスする場合は、
// その型の ComVisible 属性を true に設定してください。
[assembly: ComVisible(false)]

// 次の GUID は、このプロジェクトが COM に公開される場合の、typelib の ID です
[assembly: Guid("0eb2b52d-f7ba-4273-94f5-4ff718a785f8")]

// アセンブリのバージョン情報は、以下の 4 つの値で構成されています:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// すべての値を指定するか、下のように '*' を使ってビルドおよびリビジョン番号を 
// 既定値にすることができます:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("7.1.1.0")]
[assembly: AssemblyInformationalVersion("7.1.1.0")]
