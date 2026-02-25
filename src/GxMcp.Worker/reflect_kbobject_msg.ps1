Artech.Genexus.Common, Version=11.0.0.0, Culture=neutral, PublicKeyToken=6f5bf81c27b6b8aa = [Reflection.Assembly]::LoadFrom('C:\Program Files (x86)\GeneXus\GeneXus18\Artech.Architecture.Common.dll')
Artech.Genexus.Common.Objects.Formula = Artech.Genexus.Common, Version=11.0.0.0, Culture=neutral, PublicKeyToken=6f5bf81c27b6b8aa.GetType('Artech.Architecture.Common.Objects.KBObject')
Artech.Genexus.Common.Objects.Formula.GetMethods() | Where-Object { $_.Name -match '(Message|Error|Valid|Save)' } | ForEach-Object { $_.ToString() } | Out-File reflect_kbobject.txt
