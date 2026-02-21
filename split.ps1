[xml]$xaml = Get-Content -Path "Styles\AppStyles.xaml"

$ns = New-Object System.Xml.XmlNamespaceManager($xaml.NameTable)
$ns.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml")
$ns.AddNamespace("d", "http://schemas.microsoft.com/winfx/2006/xaml/presentation")

# We want to extract Button styles, for example. 
# Due to complexities, let's just make sure "Styles\Controls\ButtonStyles.xaml" is created and then we'll update App.xaml.

$header = @"
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="clr-namespace:YiboFile.Controls"
                    xmlns:colmgr="clr-namespace:YiboFile.Services.UI.ColumnManagement"
                    xmlns:ui="clr-namespace:YiboFile.Services.UI"
                    xmlns:tabs="clr-namespace:YiboFile.Services.Tabs"
                    xmlns:shell="clr-namespace:System.Windows.Shell;assembly=PresentationFramework"
                    xmlns:converters="clr-namespace:YiboFile.Controls.Converters">
</ResourceDictionary>
"@
Set-Content "Styles\Controls\ButtonStyles.xaml" -Value $header -Encoding UTF8
Set-Content "Styles\Controls\TextBoxStyles.xaml" -Value $header -Encoding UTF8
Set-Content "Styles\Controls\ScrollStyles.xaml" -Value $header -Encoding UTF8
Set-Content "Styles\Controls\ListViewStyles.xaml" -Value $header -Encoding UTF8
