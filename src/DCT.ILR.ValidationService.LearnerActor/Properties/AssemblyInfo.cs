﻿using Microsoft.ServiceFabric.Actors.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("DCT.ILR.ValidationService.LearnerActor")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("DCT.ILR.ValidationService.LearnerActor")]
[assembly: AssemblyCopyright("Copyright ©  2018")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("d4556cd8-380e-4cf0-a9dc-518342995713")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: FabricTransportActorRemotingProvider(MaxMessageSize = 1073741824, RemotingListener = RemotingListener.V2Listener, RemotingClient = RemotingClient.V2Client)]
[assembly: FabricTransportServiceRemotingProvider(MaxMessageSize = 1073741824, RemotingListener = RemotingListener.V2Listener, RemotingClient = RemotingClient.V2Client)]
