# العقود والتسليم والكفالة

> المرحلة 4 من تحليل «الجنوبية» (مشروع `north`). **4.1 (الكيانات والقواعد) و4.2 (API العقود) و4.3 (API الاستلام والتنبيهات والتقارير وربط المهام) منجزة؛ 4.4 (الواجهة) و4.5 (التجربة الحية) لم تُبنَا بعد.** القرارات: [ADR-0018](../adr/0018-contracts-handover-warranty.md).

## المفهوم
على المشروع **عقد** (`Contract`) يفتحه مكتب العقود عند وصول أمر التنفيذ (معاملة الديوان اختيارياً). طبيعة التعاقد (مباشر / مناقصة / عروض أسعار) تحدد الحقول: حقول المناقصة لا تُملأ إلا لها، والعروض لا تُسجَّل في المباشر. بعد الإرساء أو الاتفاق يُدخَل الرقم الرسمي والمتعاقد معه والتاريخ والقيمة ثم يُنقل العقد إلى «ساري». تتبع العقد لجنة فنية ومالية وملاحق وضمان ومبالغ محتجزة وتقييمات للمقاول. وعند انتهاء الأعمال يطلب مهندس الموقع **استلاماً** (أولياً ثم نهائياً) فتُشكَّل لجنة ويُسجَّل الكشف والنتيجة، وملاحظات اللجنة تُتابَع حتى تُغلق.

## الكيانات (`Domain/Entities/ProjectContracts.cs` و`ProjectHandover.cs`، migration `Contracts_Foundation`)
- `Contractor`: مقاول في سجل الفرع (اسم فريد داخل الفرع، تفعيل). `EvaluationCriterion`: معيار تقييم يعرّفه الفرع (وزن اختياري).
- `Contract`: مشروع، فرع (نسخة)، معاملة الديوان واختيارياً رقم أمر التنفيذ وتاريخه، `Method`، `Status` (قيد التعاقد ← ساري ← منجز/مفسوخ، أو ملغى)، المتعاقد معه، **الرقم الرسمي (فريد داخل الفرع حين يُدخَل: `IX_Contracts_BranchNumber`)**، التواريخ والمدة والقيمة والعملة، النطاق، بيانات المناقصة، **الضمان** (أشهر وبداية ونهاية وآخر نهاية نُبّه عنها). الرقم الداخلي `CON-{سنة}-{id}` يُحسب.
- `ContractOffer`: عرض لكل متعهد (فريد)، وعرض واحد مُرسًّى عليه (`IX_ContractOffers_OneAwarded`).
- `ContractAmendment`: ملحق معتمد بترقيم داخل العقد، أثره على القيمة والمدة، ووثيقته إصدار مثبَّت. سجل لا يُعدَّل.
- `Committee` + `CommitteeMember`: لجنة فنية ومالية لعقد، أو لجنة استلام لمحضر (قيد `CK_Committees_Owner`: واحد منهما)، أعضاؤها موظفون أو أسماء خارجية (`CK_CommitteeMembers_Who`) بصفاتهم، ورئيس واحد (`IX_CommitteeMembers_OneChair`).
- `RetentionEntry` + `RetentionDecision`: مبلغ محتجز (يدوي) وقراراته: إفراج (نهائي) أو استمرار احتجاز بتاريخ وجهة اعتماد ووثيقة اختيارية.
- `ContractorEvaluation` + `EvaluationScore`: تقييم بمسار مسودة ← مرسل ← مراجَع، ودرجات يدوية 0–100 مع نسخة من اسم المعيار ووزنه.
- `HandoverRecord` (أولي/نهائي؛ طلب ← لجنة مشكَّلة ← منتهٍ بنتيجة، أو ملغى) + `HandoverNote` (ملاحظة مفتوحة ← عولجت ← تُحقِّق منها). طلب واحد غير منتهٍ لكل نوع (`IX_HandoverRecords_OneOpenPerKind`) واستلام نهائي واحد بلا ملاحظات لكل مشروع (`IX_HandoverRecords_OneFinalConcluded`).
- الكيانات كلها `Restrict` بلا حذف متسلسل، والمبالغ `decimal(18,2)`، ولا `HasDefaultValue` على bool أو enum.

## القواعد الصرفة (`Application/Features/Contracts/ContractRules.cs`، `Application/Features/Handover/HandoverRules.cs`)
- **الحدّ:** كل صلاحية عقدية أو استلامية تحتاج رؤية المشروع (`ProjectRules.Visible`) + `AnyContractView`/`AnyHandoverView`؛ المقاولون والمعايير: فرع المستخدم أو نظرة المؤسسة.
- **التجمّد:** بعد «ساري» لا تتغير الطبيعة ولا المتعاقد ولا القيمة ولا المدة (ملحق فقط)؛ المفسوخ والملغى لا يُعدَّلان. السريان يشترط الرقم الرسمي والمتعاقد والتاريخ والقيمة، وللمناقصة وعروض الأسعار عرضاً مُرسًّى عليه هو عرض المتعاقد معه.
- **الملاحق:** على عقد ساري أو منجز بعنوان وتاريخ لا في المستقبل وأثر أو شرح؛ القيمة والمدة الفعلية جمع بسيط ولا تنزلان إلى الصفر.
- **المحتجز:** مبلغ موجب على عقد ساري أو منجز؛ القرار يشترط جهة اعتماد وتاريخاً بين الاحتجاز واليوم؛ الإفراج نهائي؛ الحالة من آخر قرار بالتاريخ. لا معادلة.
- **التقييم:** مشارك في المشروع (مدير النظام مستثنى) على عقد ساري أو منجز؛ الإرسال يشترط درجة واحدة؛ المراجع غير المُعدّ حتى مدير النظام؛ لا مجموع ولا تصنيف.
- **الاستلام:** مرحلة التنفيذ فصاعداً؛ «بملاحظات» تشترط ملاحظة مسجَّلة؛ النهائي «بلا ملاحظات» يشترط إغلاق ملاحظات المشروع؛ المتحقق غير من عالج الملاحظة حتى مدير النظام؛ `ClosureWarnings` تعرض ما لم يكتمل ولا تمنع الإغلاق.
- **الضمان:** `IsWarrantyEndingSoon`/`NeedsWarrantyNotice` مرشّحا تنبيه (مرة لكل نهاية)؛ نهاية الضمان تُقترح (`SuggestedWarrantyEnd`) ولا تُكتب تلقائياً.

## الصلاحيات (22، `AppPermissions.cs`)
عقود: `ViewContracts`, `CreateContract`, `EditContract`, `ManageContractCommittees`, `AddContractAmendment`, `RecordContractRetention`, `ExportContractsReport` · مقاولون: `View/Create/Edit/DeleteContractor` · تقييم: `ViewContractorEvaluations`, `EvaluateContractor`, `ReviewContractorEvaluation` · معايير: `View/Create/Edit/DeleteEvaluationCriterion` · استلام: `ViewHandovers`, `RequestHandover`, `ManageHandover`, `FollowHandoverNotes`. مجموعات «أيّ من»: `AnyContractView`, `AnyContractorView`, `AnyEvaluationCriterionView`, `AnyHandoverView` (والتقرير ضمن `AnyReportExport`).

## حراسات الحذف
- فرع له مقاولون أو معايير، ومستخدم له أثر عقدي (إنشاء عقد، عرض، ملحق، لجنة، محتجز، قرار، تقييم، استلام، ملاحظة): يُرفض بـ«ما له أثر» (`IProjectService.ExistsForBranchAsync/ExistsForUserAsync`).
- وثيقة مثبَّتة في سجل عقد أو استلام لا تُحذف (`DeleteDocumentAsync`): أضف إصداراً جديداً.
- حذف مقاول أو معيار مستعمل: يُبنى مع الـ API (4.2).

## الـ API (4.2)
بنية معالج لكل حالة استخدام (`Application/Features/Contracts/`): `ContractContext` (التحميل والحراسة والإشعار)، `ContractSupport` (ربط الـ DTO وأعلام الأزرار بالقواعد نفسها)، `UseCases/` (`Contractors.cs`, `Contracts.cs`, `ContractOffers.cs`, `ContractRecords.cs`, `ContractEvaluations.cs`)، و`IContractService` ← `Infrastructure/Persistence/Repositories/ContractService.cs` (كل كتابة مع أثرها في المعاملة نفسها، وrowVersion يرفض التعديل فوق تعديل غيره، وفهارس الفرادة تُترجم إلى رسائل عربية).

| المسار | الصلاحية | ما يفعل |
|--------|----------|---------|
| `GET api/Contracts/Search` · `Get/{id}` | `AnyContractView` | قائمة مرقّمة بفلاتر (مشروع، فرع، حالة، طبيعة، مقاول، بحث، «ينتهي ضمانه قريباً») وتفصيل بأعلام الأزرار |
| `POST Create` · `PUT Update/{id}` · `POST Status/{id}` | `CreateContract` / `EditContract` | فتح العقد وتعديله وتغيير حالته بقرار (السريان يشترط الاكتمال) |
| `POST Offers/{contractId}` · `POST Award/{offerId}` | `EditContract` | عروض المناقصة أو الأسعار، والإرساء يحدّد المتعاقد (ويُنسخ مبلغ العرض إن لم تكن للعقد قيمة) |
| `POST Amendments/{contractId}` | `AddContractAmendment` | ملحق معتمد (سجل يُضاف) |
| `POST Committees/{contractId}` · `PUT Committee/{id}` | `ManageContractCommittees` | اللجنة الفنية والمالية؛ التعديل يستبدل الأعضاء ويحفظ ما كان وما صار في الأثر |
| `POST Retention/{contractId}` · `POST RetentionDecision/{entryId}` | `RecordContractRetention` | مبلغ محتجز يدوي، وقرار إفراج (نهائي) أو استمرار |
| `POST Evaluations/{contractId}` · `PUT/DELETE Evaluation/{id}` · `POST Evaluation/{id}/Submit` | `EvaluateContractor` | مسودة التقييم وتعديلها وحذفها وإرسالها (يُنبَّه حاملو صلاحية المراجعة) |
| `POST Evaluation/{id}/Review` · `Return` | `ReviewContractorEvaluation` | حفظ في ملف المقاول، أو إعادة بسبب لمُعدّه |
| `api/Contractors`: `GetAll` · `Create` · `Update/{id}` · `Delete/{id}` · `Evaluations/{id}` | `AnyContractorView` / `CreateContractor`… / `ViewContractorEvaluations` | مقاولو الفرع وملف المقاول (المراجَع من تقييماته على عقود يراها) |
| `api/EvaluationCriteria`: `GetAll` · `Create` · `Update/{id}` · `Delete/{id}` | `AnyEvaluationCriterionView` / `CreateEvaluationCriterion`… | معايير التقييم (وزن اختياري، لا تصنيف) |

- **من يرى التقييم:** صاحب «عرض التقييمات» كل ما على عقود يراها؛ ومن يعدّ ما أعدّه هو؛ ومن يراجع المرسَل والمراجَع (لا مسودات غيره). غير المرئي يُعامل كغير موجود (404).
- **الإرساء:** في التنفيذ المباشر يُدخَل المتعاقد معه مباشرة؛ في غيره يُمنع إدخاله ويحدّده الإرساء، ولا تتغير طبيعة العقد بعد تسجيل عروض.
- **الإشعارات:** `ContractEvaluationSubmitted/Reviewed/Returned` (نوع الربط `Contract`). تنبيه الضمان في 4.3.
- **الأثر:** `AuditEntities.Contract/Contractor/EvaluationCriterion/ContractorEvaluation`، وكل سجل فرعي (عرض، ملحق، لجنة، محتجز، قرار) يُكتب على العقد.
- **المصفوفة والعقد:** `permission-matrix.txt` (+27 نقطة) و`contract/permissions.json` و`contract/openapi.json`.
- **توليد لقطة OpenAPI بلا Docker:** مشروع تجميع صغير (بلا قاعدة بيانات) يبني الخدمات نفسها ويكتب `ISwaggerProvider.GetSwagger("v1")` بالترتيب نفسه؛ طُوبقت مخرجاته مع اللقطة القائمة سطراً بسطر (إضافات فقط) قبل اعتماده. الاعتماد الأساسي يبقى على اختبار `OpenApiContractTests` في CI.

## الاستلام والتنبيهات والتقارير (4.3)
بنية معالج لكل حالة استخدام (`Application/Features/Handover/`): `HandoverContext`، `HandoverSupport`، `UseCases/` (`HandoverRecords.cs`, `HandoverCommittee.cs`, `HandoverNotes.cs`)، و`IHandoverService` ← `HandoverService.cs`. أعضاء اللجان مشتركون مع العقود (`CommitteeInput`).

| المسار `api/Handovers` | الصلاحية | ما يفعل |
|--------|----------|---------|
| `GET Search` · `Get/{id}` · `ClosureWarnings/{projectId}` | `AnyHandoverView` | قائمة مرقّمة (مشروع، فرع، نوع، حالة، «فيها ملاحظات مفتوحة») وتفصيل بأعلام الأزرار وإنذار إغلاق المشروع |
| `POST Request` | `RequestHandover` | طلب أولي أو نهائي (مرحلة التنفيذ فصاعداً؛ طلب واحد غير منتهٍ لكل نوع؛ معاملة واردة اختيارية) |
| `POST Committee/{id}` · `PUT Committee/{id}` · `POST Conclude/{id}` · `POST Cancel/{id}` | `ManageHandover` | تشكيل اللجنة (rowVersion للمحضر) وتعديلها (rowVersion للجنة، تستبدل الأعضاء)، وتسجيل الكشف والنتيجة والمحضر وتقرير الإشراف، والإلغاء |
| `POST Notes/{id}` · `PUT/DELETE Note/{id}` · `POST Note/{id}/Fix` · `Verify` · `Reject` | `FollowHandoverNotes` | ملاحظات اللجنة وإسنادها ومعالجتها والتحقق منها (غير من عالج) أو رفض المعالجة بسبب |

- **النتيجة:** «بملاحظات» تشترط ملاحظة مسجَّلة؛ «بلا ملاحظات» تُرفض والسجل فيه ملاحظات، وفي النهائي والمشروع فيه ملاحظات مفتوحة في سجلات أخرى؛ والنهائي بلا ملاحظات واحد لكل مشروع.
- **الإنذار قبل الإغلاق** (`ClosureWarnings`): لا استلام نهائي بلا ملاحظات، ملاحظات لم تُغلق، طلب لم ينتهِ، محتجز لم يُفرج عنه، عقد ساري — **تنبيه لا منع**؛ الإغلاق قرار على مرحلة المشروع.
- **التذكيرات** (في `TaskReminderWorker`، تكرارها آمن):
  - `ContractReminders`: ضمان ينتهي خلال 30 يوماً (`ContractContext.WarrantyWindowDays`) ← يُنبَّه من أنشأ العقد وحاملو `EditContract` ممن يرون مشروعه، مرة لكل نهاية ضمان (`WarrantyNotifiedFor`).
  - `HandoverReminders`: ملاحظة لم تُغلق وتجاوزت موعدها ← مسؤولها (إن كان موظفاً) وحاملو `FollowHandoverNotes`، مرة لكل موعد (`HandoverNote.OverdueNotifiedFor`، migration `Handover_Note_Overdue_Notice`).
  - لا تتغير حالة العقد ولا الملاحظة ولا المحتجز بالتنبيه.
- **الإشعارات:** `ContractWarrantyEnding`, `HandoverRequested`, `HandoverConcluded`, `HandoverNoteAssigned`, `HandoverNoteFixed`, `HandoverNoteOverdue` (ربط `Contract` أو `HandoverRecord`).
- **التقارير** (صلاحية `ExportContractsReport`): `contracts` (الحالة والطبيعة والمتعاقد والقيمة بعد الملاحق والضمان ونهايته وأيامه والمحتجز غير المفرج عنه، بفلاتر الحالة والطبيعة والفترة والوحدة)، و`contractor-evaluations` (التقييمات المراجَعة المحفوظة؛ تشترط فوقها `ViewContractorEvaluations`، بلا مجموع ولا تصنيف).
- **ربط المهام:** نوعا ربط جديدان «عقد» (`CON-…`) و«استلام» (`HND-…`) في `TaskLinkResolver`: الربط والعرض مشروطان برؤية السجل نفسه.
- **ما لم يُبنَ:** لا تثبيت آلي لوثائق في الإضبارة (تُرفع من شاشة الوثائق وتُربط بإصدارها المثبَّت)، ولا تقرير للمناقصات منفصل (بيانات المناقصة وعروضها في تقرير العقود وتفصيل العقد).

## الاختبارات
- وحدة: `ContractRulesTests.cs` و`HandoverRulesTests.cs` (61 اختباراً)، مع فحص طفرات على قواعد الفصل بين المُعدّ والمراجع، وإفراج المحتجز النهائي، وفحص السريان، و«بملاحظات».
- تكامل: `tests/Integration.Tests/ContractTests.cs` (10 اختبارات: المقاولون والمعايير، العقد المباشر وتجمّده، الملاحق، اللجنة، المحتجز، المناقصة، مسار التقييم، مسودة التقييم، حدّ الرؤية، الضمان) و`HandoverTests.cs` (6 اختبارات: الطلب واللجنة، النتيجة والملاحظات وفصل المعالِج عن المتحقق، النهائي وإغلاق الملاحظات وإنذار الإغلاق، الحدود، تذكير الملاحظات، تذكير الضمان والتقرير) — **كُتبت وتُترجَم، ولم تُشغَّل بعد** (Docker متوقف وقت الكتابة)؛ تُشغَّل في CI أو محلياً حين يعمل Docker.

## المتبقي
- **4.4:** الواجهة (صفحات العقود والمقاولين والمعايير والاستلام، تبويبات المشروع، مسار التقييم، التنبيهات والإشعارات، نوعا ربط المهام) ومزامنة `contract/` وأنواع TypeScript وصلاحيات الواجهة.
- **4.5:** التجربة الحية على بيانات الاختبار، وتشغيل اختبارات التكامل حين يعمل Docker.
