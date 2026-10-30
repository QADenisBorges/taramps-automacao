# Escolhe a conta QADenisBorges na janela "Select an account" do Git Credential Manager
# e clica em Continue. Usar com o git push rodando em segundo plano.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$p = Get-Process git-credential-manager | Where-Object { $_.MainWindowTitle -ne '' } | Select-Object -First 1
$root = $A::FromHandle($p.MainWindowHandle)
$cond = New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
$item = $null
foreach ($li in $root.FindAll($TS::Descendants, $cond)) {
  $t = $li.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, 'QADenisBorges')))
  if ($t) { $item = $li }
}
if (-not $item) { 'conta nao encontrada'; exit 1 }
$sel = $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
$sel.Select()
Start-Sleep -Milliseconds 400
"selecionada: " + $sel.Current.IsSelected
$btn = $root.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::NameProperty, 'Continue')))
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
'Continue clicado'
