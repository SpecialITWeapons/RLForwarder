using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;
using System;

namespace RLForwarder
{
    [RunInstaller(true)]
    public partial class ServiceInstaller : System.Configuration.Install.Installer
    {
        public ServiceInstaller()
        {
            
            ServiceInstaller serviceInstaller = new ServiceInstaller(

                );
            ServiceProcessInstaller processInstaller = new ServiceProcessInstaller();

            // Konfiguracja konta usługi
            processInstaller.Account = ServiceAccount.LocalSystem;

            // Nazwa usługi, jak będzie widoczna w Menadżerze Usług
            serviceInstaller.ServiceName = "MyServiceName";
            serviceInstaller.DisplayName = "My Service Display Name";
            serviceInstaller.StartType = ServiceStartMode.Manual;  // lub Auto

            this.Installers.Add(processInstaller);
            this.Installers.Add(serviceInstaller);
        }
    }
}