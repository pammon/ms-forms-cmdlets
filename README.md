# ms-forms-ps-module

## Example
> Connect

Connect-MsForms -TenantId "" -ClientId "" -Credentials (Get-Credential)

> get forms from a user

$forms = Get-MsForms -UserId ""

$forms | select -ExpandProperty Title


## Install
> Install-Module -Name FlowSoft.OfficeFormsModule


## setup
### register AAD App
Go to Azure > Active Directory > App registrations > New registration <br/>
Name: PsOfficeFormsCmdlet or what you want<br/>
<b>Redirect Uri: Public client/..</b><br/>
<img src="/assets/img_1.png" /> <br/>

#### Authentication
<img src="/assets/img_2.png" /> <br/>
<img src="/assets/img_3.png" /> <br/>
<img src="/assets/img_4.png" /> <br/>
<img src="/assets/img_5.png" /> <br/>

#### Grant Permissions
<img src="/assets/img_6.png" /> <br/>
<img src="/assets/img_7.png" /> <br/>
<img src="/assets/img_8.png" /> <br/>
<img src="/assets/img_12.png" /> <br/>
<img src="/assets/img_13.png" /> <br/>

> Grant admin consent


