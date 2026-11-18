# اختبارات الجهات المستفيدة للدراسات

من جذر الباك:

```powershell
dotnet test tests/StudyBeneficiaries.Tests/StudyBeneficiaries.Tests.csproj
```

تتحقق الاختبارات من إدارة الجهات وصلاحياتها، منع حذف جهة مستخدمة، استبعاد الجهات المعطلة من الطلبات الجديدة،
وحفظ اسم الجهة في الطلب القديم بعد التعطيل أو إعادة التسمية. تستخدم قاعدة EF في الذاكرة وتوليد SQL الترحيل فقط،
ولا تتصل بقاعدة EWMS أو تطبق الترحيلات عليها.

تتحقق أيضاً من إلزامية رمز الجهة وتفرّده وطوله، ومن استخدام الرمز المسجّل للجهة عند حفظ الطلب
بدلاً من رمز يرسله العميل، ومن إكمال الجهات القديمة التي ينقصها رمز.

إذا كانت ملفات API قيد الاستخدام، يمكن عزل مخرجات البناء:

```powershell
dotnet test tests/StudyBeneficiaries.Tests/StudyBeneficiaries.Tests.csproj --artifacts-path artifacts/study-beneficiaries-tests --disable-build-servers -m:1 -p:UseSharedCompilation=false
```
