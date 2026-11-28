# واجهات فرع الدراسات المركزية — دليل الواجهة الأمامية

كل الواجهات تحت `api/` وتحتاج رمز الدخول (`Authorization: Bearer …`). الأخطاء بالشكل المعتاد في النظام:
`{ statusCode, message }` (و`errors` لأخطاء التحقق). الرسائل عربية جاهزة للعرض.

- **400**: قاعدة عمل أو تحقق من المدخلات.
- **403**: لا صلاحية، أو السجل خارج فرعك.
- **404**: غير موجود.
- Swagger في بيئة التطوير: `/swagger`.

## قواعد عامة

- **الحدود:** كل صلاحيات الدراسات محصورة بدراسات فرع المستخدم، ومن يملك `ViewOrganizationDashboard` يرى كل الفروع.
- **الأعلام:** كل مخرَج يحمل أعلاماً جاهزة مثل `canEdit` و`canDelete` و`canSubmit` و`canCancel` و`canCreateAssignment`، وفي سطور التوقيع `canDecide`. أظهر الأزرار حسبها، ولا تعِد حساب القواعد في الواجهة.
- **التعدادات:** كل تعداد يأتي رقماً مع اسمه العربي (`status` و`statusName`). القوائم كلها في `GET api/StudyLookups/Enums`.
- **التعديل المتزامن:** أرسل `rowVersion` الذي استلمته مع كل تعديل. إن عدّل غيرك السجل بعد فتحه يعود 400 برسالة «عدّل مستخدم آخر…»، فأعد التحميل.
- **القوائم المقسّمة صفحات:** `PagedResultDto` = `{ items, totalCount, page, pageSize, totalPages }`، والحد الأقصى `pageSize ≤ 100`. فلاتر البحث المشتركة: `status`، `search`، `branchId` (لنظرة المؤسسة)، `from`، `to`، `page`، `pageSize`.
- **التواريخ:** التواريخ بلا وقت (`yyyy-MM-dd`).

## سير العمل

```text
طلب (REQ) ─Submit→ معتمد
  └─ تكليف IPA ─Submit→ رئيس الفرع ─→ معتمد
       └─ رد ─Submit→ القسم المتابع → التخطيط وإدارة المشاريع ─→ معتمد
            └─ معاملة الديوان DWN (الصادر + الرد المركزي وصورة كتابه) ─Submit→ رئيس الفرع ─→ معتمدة بقرار الديوان المركزي
                 └─ أمر بدء PIO (بقرار موافقة، وإن لم تكن التوصية «غير متاح») ─Submit→ رئيس القسم المختص → رئيس الفرع ─→ معتمد
                      └─ مشروع: مراحل + أقسام مشاركة + فريق + مهام + مخططات
                           └─ RequestCompletion → إدارة المشاريع → رئيس الفرع ─→ مكتمل
```

هذه سلسلة **المركزية**. طلب فرع جغرافي يسير هكذا (انظر «أفرع الدراسات والمسار الجغرافي» أدناه):

```text
طلب (فرع دراسة جغرافي) ─Submit→ معتمد
  └─ إحالة إلى الفرع: StudyProjects/CreateRegional (مشروع تنسيق ومتابعة، بلا تكليف ولا رد ولا ديوان ولا أمر بدء)
       └─ مراحل مسار التنسيق يغلقها التنسيق والارتباط مع تقدّم الفرع
            └─ RequestCompletion → التنسيق والارتباط → ضبط الجودة ─→ مكتمل
```

### أفرع الدراسات والمسار الجغرافي (2026-11-28)

- **فرع الدراسة ≠ فرع النظام.** أفرع الدراسات (المركزية 0، الشمالية 1، الجنوبية 2، الوسطى 3، الغربية 4، الشرقية 5) ليست فروعاً في النظام، بل قائمة تحت فرع الدراسات (`GET api/StudyLookups/StudyBranches/GetAll`). فرع النظام (`branchId`) يبقى حدّ الصلاحيات كما هو.
- **المسار (`track`):** 1 دورة كاملة (المركزية)، 2 تنسيق ومتابعة (الأفرع الجغرافية، وهي أقسام الدراسات في الفروع الجغرافية بلا ربط بالنظام الآن). يأتي في الطلب والمشروع والسلسلة (`chain.track`)، والقيم في `Enums.track`.
- **نموذج الطلب:** الحقل «الفرع» القديم (`branchId`، كان لنظرة المؤسسة فقط) **حُذف**. مكانه **«فرع الدراسة» `studyBranchId` إلزامي لكل المستخدمين**، من `StudyBranches/GetAll?activeOnly=true` (فرع المستخدم، أو كل الفروع لنظرة المؤسسة). الخادم يأخذ فرع الطلب في النظام من فرع الدراسة المختار. يتغير في المسودة فقط.
- **الخطوة التالية بعد تسجيل الطلب:**
  - المركزية: `canCreateAssignment` كما كان.
  - الفرع الجغرافي: `canCreateRegionalProject` ← زر «إحالة إلى الفرع» يفتح نموذج `POST api/StudyProjects/CreateRegional`. التكليف ومعاينة كوده يرفضان طلب الفرع الجغرافي (400)، والإحالة ترفض طلب المركزية (400).
- **كود المشروع يبدأ برمز فرع الدراسة** (0–5) لا برقم الفرع في النظام: `رمز فرع الدراسة-سنة التكليف أو الإحالة-N/R/RN-رقم الطلب-رقم أرشيف ديوان الإدارة`. الأكواد التجريبية القديمة صُحّحت إلى `0-…`.
- **مشروع الفرع الجغرافي:**
  - `startOrderId`/`startOrderNumber` = `null`، و`requestId`/`requestNumber`/`requestStatus` موجودة لكل مشروع.
  - مراحله من أنواع المراحل ذات `track = 2` فقط (مشاريع المركزية من `track = 1` فقط)، وقسم من أحال (التنسيق والارتباط) مسؤول عنها ومشارك في المشروع. **بلا نوع مرحلة فعّال لمسار التنسيق تُرفض الإحالة** — عرّف الأنواع أولاً.
  - **لا مخططات** (400): ما يرد من الفرع يُرفع مرفقات للمشروع. أخفِ تبويب المخططات حين `track = 2`.
  - إنجازه: سطرا توقيع `signerRole` 9 «التنسيق والارتباط» ثم 7 «ضبط الجودة».
  - إعادة التفعيل تتبع الطلب (لا أمر بدء)، والطلب لا يُلغى ما دام له مشروع غير ملغى.
- **فلتر:** `studyBranchId` في `StudyRequests/GetAll` و`StudyProjects/GetAll`.

- **حالات المستند:**
  - 1 مسودة؛
  - 2 بانتظار الاعتماد؛
  - 3 معتمد؛
  - 4 مرفوض: يُعدَّل فيعود مسودة ثم يُرسل من جديد؛
  - 5 ملغى: يبقى للأرشيف.
- **الحذف والإلغاء:** يُحذف المستند وهو مسودة فقط. المعتمد أو المعلّق يُلغى بسبب (`Cancel`)، بشرط ألا يكون له مستند تالٍ قائم.
- **التواقيع بالترتيب:**
  - الرفض من أي موقّع يرفض المستند، واكتمال الموافقات يعتمده.
  - لا يوقّع الشخص مرتين على المستند نفسه.
  - التوقيع بصفة «القسم المتابع/المختص/المرسل» لموظفي ذلك القسم فقط.
- **بانتظار توقيعي:** `GET api/StudyApprovals/Pending` يعيد ما يستطيع المستخدم توقيعه الآن. اعرضه كصندوق وارد.

## الواجهات

### القوائم — `api/StudyLookups`

| الطريقة | المسار | الصلاحية | ملاحظة |
|---|---|---|---|
| GET | `Enums` | ViewStudyLookups | كل التعدادات |
| GET/POST/PUT/DELETE | `StageTypes/GetAll?activeOnly`، `StageTypes/Create`، `StageTypes/Update/{id}`، `StageTypes/Delete/{id}` | View/Create/Edit/DeleteStudyLookup | `linkedDocument`: 0 لا شيء، 1 طلب، 2 تكليف، 3 رد، 4 أمر بدء. `track` (2026-11-28): 1 مشاريع المركزية (الافتراضي)، 2 مشاريع الأفرع الجغرافية — ولها `linkedDocument` 0 أو 1 فقط؛ لا يتغير المسار لنوع مستخدم |
| GET/POST/PUT/DELETE | `AttachmentTypes/…` بالنمط نفسه | العرض: أي صلاحية عرض للدراسات | |
| GET/POST/PUT/DELETE | `Disciplines/…` بالنمط نفسه | ViewStudyLookups… | |
| GET/POST/PUT/DELETE | `Beneficiaries/…` بالنمط نفسه | القراءة: أي صلاحية للقوائم View/Create/Edit/DeleteStudyLookup أو Create/EditStudyRequest؛ الإدارة: Create/Edit/DeleteStudyLookup | اسم حتى 200 حرف + رمز `code` مطلوب وفريد حتى 30 حرفاً + `isActive`؛ و`activeOnly=true` يعيد الجهات الفعالة ذات الرمز المكتمل |
| GET | `Employees?branchId&departmentId` | `AnyStudyEmployeePick` (أيٌّ من: Create/EditStudyResponse، Create/EditStudyStartOrder، EditStudyProject، AddStudyProjectMember، CreateRegionalStudyProject) | موظفو فرع الدراسة لقوائم الاختيار — انظر تحت الجدول |
| GET | `StudyBranches/GetAll?branchId&activeOnly` | `AnyStudyBranchPick` (قوائم الدراسات، Create/EditStudyRequest، ViewStudyRequests، ViewStudyProjects) | أفرع الدراسات: `{ id, branchId, branchName, code, name, track, trackName, isActive, inUse, rowVersion }` |
| POST/PUT/DELETE | `StudyBranches/Create`، `StudyBranches/Update/{id}`، `StudyBranches/Delete/{id}` | Create/Edit/DeleteStudyLookup | الجسم `{ branchId?, code, name, track, isActive }` — `branchId` لنظرة المؤسسة عند الإنشاء فقط؛ الرمز 0–99 فريد في الفرع، والاسم كذلك. **`inUse` (له طلبات): لا يتغير الرمز ولا المسار ولا يُحذف** (عطّله) |

**`Employees`: قائمة اختيار الموظفين بلا `ViewUsers`:**

- **الاستعمال:** مدير المشروع المقترح في الرد، ومدير المشروع والمهندسون في أمر البدء، وتغيير مدير المشروع، وإضافة أعضاء القسم المشارك (مع `departmentId` القسم المشارك).
- **الحقول:** يعيد الموظفين **النشطين** فقط، بالحقول `id`، `fullName`، `departmentId`، `departmentName`، `officeId`، `officeName`. لا بريد ولا هاتف ولا رقم ذاتي ولا دور.
- **الترتيب:** بالقسم ثم الاسم.
- **الحد:** فرع المستخدم دائماً.
  - من يتصفح المؤسسة يمرّر `branchId` (فرع الدراسة) أو `departmentId`، ودونهما يعود 400.
  - فرع أو قسم خارج حدك = 403.
  - قسم ليس من الفرع المحدد = 400.
  - مستخدم بلا فرع = قائمة فارغة.
- **مطابقة الخادم:** ما في القائمة هو ما يقبله الخادم. مدير المشروع المقترح ومدير المشروع والمهندسون يجب أن يكونوا موظفين نشطين من فرع الدراسة، وإلا يعود 400 «… ليس من موظفي فرع الدراسة»، أو 404 للموظف غير النشط. والعضو يجب أن يكون من القسم المشارك نفسه.

### المستندات

**الجهة المستفيدة في الطلب:** يرسل الإنشاء والتعديل `mainBeneficiaryId` لجهة فعالة من قائمة الجهات المستفيدة.
يأخذ الخادم اسمها ورمزها من القائمة ويحفظهما في `mainBeneficiary` و`entityCode`؛ لا يعتمد الاسم أو الرمز الذي يرسله العميل.
حقل الرمز في الطلب يُملأ تلقائياً ويُعرض للقراءة فقط؛ الأصفار في بداية الرمز تبقى محفوظة لأن الرمز نص.
يمكن إبقاء الجهة نفسها عند تعديل طلب قديم حتى بعد تعطيلها أو تغيير اسمها، ويبقى الاسم الأصلي في المستند.
الجهة المستخدمة في طلبات لا تُحذف، بل تُعطّل. الترحيل `Study_Beneficiaries` يستورد أسماء الجهات الحالية ويربط الطلبات بها.
الترحيل `Study_Beneficiary_Codes` يستعيد الرمز من الطلبات القديمة إذا كان رمزاً واحداً للجهة وغير مشترك مع جهة أخرى؛
الرموز الناقصة أو المتعارضة تستكمل من الإعدادات قبل اختيار الجهة أو حفظ تعديل طلبها. لا يغيّر الترحيل رموز المستندات السابقة.

المستندات هي `api/StudyRequests` و`api/StudyAssignments` و`api/StudyResponses` و`api/StudyDiwanTransactions` و`api/StudyStartOrders`، ولكل منها المسارات نفسها:

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `GetAll` (فلاتر البحث) | View… |
| GET | `Get/{id}` — التفاصيل مع `chain` (روابط السلسلة كلها) و`approvals` | View… |
| POST | `Create` | Create… |
| PUT | `Update/{id}` | Edit… |
| DELETE | `Delete/{id}` | Delete… |
| PUT | `Submit/{id}` | Edit… |
| PUT | `Cancel/{id}` بالجسم `{ "reason": "…" }` | CancelStudyDocuments |

- **الإنشاء يحمل معرّف المستند السابق:**
  - التكليف: `requestId`؛
  - الرد: `assignmentId`؛
  - معاملة الديوان: `responseId`؛
  - أمر البدء: `diwanTransactionId` لمعاملة ديوان معتمدة بقرار موافقة مركزي (2026-11-26؛ كان `diwanReplyId`).
- **كود المشروع في التكليف:** يولّده الخادم من `رمز فرع الدراسة (0–5)-سنة التكليف (رقمان)-نوع العمل (N/R/RN)-رقم الطلب-رقم أرشيف ديوان الإدارة`. تعيد `GET api/StudyAssignments/ProjectCode/{requestId}?assignmentDate=yyyy-MM-dd` القيمة `{ projectCode }` قبل الحفظ بصلاحية إنشاء التكليف. إذا خلا الطلب من رقم أرشيف ديوان الإدارة، يُرسل `adminArchiveNumber` في المعاينة والإنشاء. يولّد الخادم الكود مجدداً عند الإنشاء. **عند تعديل مسودة التكليف** يعيد الخادم توليد الكود بسنة التاريخ الجديد، ويأخذ رقم الأرشيف من الطلب أو من `adminArchiveNumber` أو من الكود المحفوظ نفسه؛ الكود القديم المدخل يدوياً بصيغة أخرى يبقى كما هو. لمعاينة كود مسودة قائمة أضف `assignmentId` إلى طلب المعاينة (يكفي `CreateStudyAssignment` للإنشاء و`EditStudyAssignment` للتعديل). بعد الاعتماد يبقى الكود مرجعاً ثابتاً للتكليف والمشروع.
- **أجزاء الرد وأمر البدء** تُرسل كاملة في كل حفظ وتُستبدل:
  - في الرد (جزء القسم المتابع): `buildings` و`costItems` بالشكل الموحّد وحده (انظر «إشعار الرد» أدناه)؛ ومعهما `siteArea` و`initialCost` حقلان واحدان للرد كله؛
  - في أمر البدء: `engineers` و`resources`.
- **من يوقّع الآن ومن يصدر التالي (2026-11-09):** في `Get/{id}` للطلب والتكليف والرد وأمر البدء حقلان اختياريان (`null` = لا ينطبق):
  - `nextSigners` (+ `nextSignersTotal`): والمستند بانتظار الاعتماد، حاملو صلاحية أول صفة لم توقّع ضمن حدّها.
  - `nextIssuers` (+ `nextIssuersTotal`): والمستند معتمد ولم يُنجز الإجراء التالي، حاملو صلاحية الإجراء. بعد اعتماد إشعار الرد تُحسب الجهة حسب حالة معاملة الديوان.
  - كل عنصر `{ id, fullName, departmentName, jobTitle }`، مرتب بالاسم ومقصوص عند 8 والباقي في `…Total`. **المدير العام لا يظهر أبداً** (يبقى مستلماً للإشعارات). **قائمة فارغة = لا أحد يملك الصلاحية** فاعرض تنبيهاً ليراجع مدير النظام الأدوار.
- **الطلب:** تُرسل فيه `governorateCode` (SY01…SY14)، و`siteId` اختياري (من سجل المواقع)، و`studyBranchId` إلزامي (فرع الدراسة؛ حلّ محل `branchId` في 2026-11-28).

### إشعار الرد بجزأيه — `api/StudyResponses`

الرد جزءان، لكل جزء صاحبه الذي يعدّله ويوقّع عليه:

| الجزء | الحقول | صاحبه | الحفظ |
|---|---|---|---|
| القسم المتابع | `responseDate`، `proposedManagerUserId`، `recommendation`، `siteArea`، `initialCost`، `buildings`، `costItems` | موظفو القسم المتابع للتكليف (`CreateStudyResponse`/`EditStudyResponse`) | `Create`، `Update/{id}` ثم `Submit/{id}` |
| التخطيط وإدارة المشاريع | `requiredDays`، `staffEstimates`، `logisticItems` | من يملك `EditStudyResponsePlanning` في الفرع | `PUT UpdatePlanning/{id}` بالجسم `{ requiredDays, staffEstimates, logisticItems, rowVersion }` (الأجزاء تُرسل كاملة) |

- **التسلسل:**
  1. القسم المتابع يملأ جزءه ويعدّله (مسودة)، ثم يرسله ويوقّع.
  2. بعد توقيعه يُفتح جزء التخطيط للتخطيط وحده، فيملؤه ثم يوقّع، فيُعتمد الرد.
  3. لا يعدّل أي طرف جزء الآخر. بعد الإرسال يُقفل جزء القسم المتابع حتى الرفض.
- **الأعلام** (في `Get/{id}`؛ وفي القوائم يأتي `canEditPlanningPart` دائماً false): `canEditFollowingPart` (مسودة أو مرفوض، من قسم التكليف)، و`canEditPlanningPart` (وقّع القسم المتابع ولم يوقّع التخطيط بعد). `canEdit` = أحدهما، و`canSubmit` = `canEditFollowingPart`.
- **الرفض:** من أي موقّع يعود الرد إلى القسم المتابع (حالة 4 مرفوض): يعدّل جزءه فيعود مسودة، وقيم جزء التخطيط تبقى محفوظة، وعند إعادة الإرسال تُصفَّر التواقيع وتبدأ السلسلة من جديد.
- **التوصية «غير متاح»:** حقول جزء التخطيط اختيارية دائماً، والتواقيع تبقى.
- **حدّ القسم المتابع:** إنشاء الرد وتعديله وإرساله لموظفي قسم التكليف المتابع فقط (من غيرهم 403)، وجزء التخطيط حدّه الفرع.
- **الرقم:** `responseNumber` بصيغة `RSP-26-0001` من عدّاد الترقيم (البادئة مؤقتة، ثابت واحد `StudyRules.ResponseNumberPrefix`). مساحة الموقع والتكلفة المبدئية انتقلتا من سطور المباني إلى الرد (`siteArea`، `initialCost`)، و`totalCost` مجموع جدول البنود يُحسب منفصلاً.
- **الطلب:** حقل `requestNumber` إلزامي في `Create`/`Update`: رقم طلب تجهيز الدراسة كما ورد من الديوان المركزي، نص حتى 30 حرفاً يحافظ على الأصفار والشرطات. لا يُولَّد من عدّاد النظام. يُرفض الرقم الفارغ أو المكرر، ويُستخدم الرقم المدخل في القوائم والتكليفات والطباعة. يبقى `diwanRequestNumber` حقلاً قديماً للتوافق مع البيانات السابقة ولا يلزم إدخاله للطلبات الجديدة.

### الشكل الموحّد للمباني وبنود التكلفة (2026-11-26)

الخادم يرفض أي شكل آخر (400 مع رسالة عربية في `errors`):
- لكل نوع مبنى (1 إعادة تأهيل، 2 إنشاء جديد): `buildingCount` من 1 إلى 200 = عدد أسطر `details`، ولكل سطر `code` و`function` و`area` (> 0). `buildingsArea` يحسبها الخادم مجموعَ مساحات الأسطر (القيمة المرسلة لا تُعتمد).
- `costItems`: بند واحد لكل سطر تفاصيل، بالترتيب نفسه (النوع 1 ثم 2، وداخل النوع بترتيب الأسطر): `description` = «النوع — الرمز — الوظيفة» (الفاصل « — » شرطة طويلة بين مسافتين)، `unit` = `م²`، `quantity` = مساحة المبنى، و`unitPrice` حر. رد بلا مبانٍ = بلا بنود.
- الترحيل `Study_Response_Unified_Buildings` حوّل الردود القديمة مرة واحدة: المبنى بلا تفاصيل صار أسطراً رموزها `M1` `M2`… ووظيفتها «غير محددة» والمساحة بالتساوي، وبند لكل مبنى بسعر متر يحفظ إجمالي الرد القديم (فرق تقريب سعر المتر بخانتين فقط). نسخة من الأسطر القديمة في الجداول `StudyResponseLegacy*` حتى التحقق.

### معاملة الديوان — `api/StudyDiwanTransactions` (2026-11-26)

مستند كباقي المستندات (المسارات والحالات والإلغاء والحذف نفسها، الجدول أعلاه)، واحد لكل إشعار رد، رقمه `DWN-yy-nnnn`، وخطوته في `chain` دائماً بعد الرد: `diwanId` و`diwanNumber` و`diwanStatus` و`centralDecision` (فارغة ما لم تُنشأ). حلّ محل المسارات `api/StudyResponses/Diwan/{id}/…` وصفحة الديوان الخاصة (حُذفت، وحُذف `diwanStage` وفلاتر `diwanStage`/`diwanOnly`/`diwanOpen` من قائمة الردود).

| الخطوة | الطلب | الشرط |
|---|---|---|
| تسجيل الصادر | `POST Create` ‏`{ responseId, dispatchNumber, dispatchDate, notes? }` | رد معتمد بلا معاملة ولا أمر بدء (`response.canCreateDiwanTransaction`) |
| تسجيل الرد المركزي | `PUT Update/{id}` ‏`{ dispatchNumber, dispatchDate, replyNumber, replyDate, centralDecision, notes, rowVersion }` | مسودة أو مرفوضة، والرد معتمد. `centralDecision`: 1 موافقة، 2 رفض |
| صورة الكتاب | `POST api/StudyAttachments/Upload?ownerType=14&ownerId={id}&attachmentTypeId=…` (نوع «كتاب رسمي») | مرفق عادي يظهر مع مرفقات الدراسة |
| الإرسال للاعتماد | `PUT Submit/{id}` | رقم الرد المركزي وتاريخه وقراره، ومرفق واحد على الأقل |
| توقيع رئيس الفرع | `PUT api/StudyApprovals/Decide/{approvalId}` (يظهر في `Pending` و`ByOwner?ownerType=14`) | `SignStudyAsBranchHead` في فرع الدراسة |

- **أثر التوقيع:** موافقة رئيس الفرع تُثبت قرار الديوان المركزي:
  - قرار موافقة: المعاملة معتمدة، و`canCreateStartOrder` يفتح أمر البدء (`POST api/StudyStartOrders/Create` بـ `diwanTransactionId`)، إلا مع التوصية «غير متاح» فتُغلق السلسلة بلا أمر بدء.
  - قرار رفض: المعاملة «مرفوضة» ويعود الرد «مرفوضاً» إلى القسم المتابع مع إشعاره. بعد اعتماد الرد المعدّل تُعدَّل **المعاملة نفسها** بصادر ورد جديدين وتُرسل من جديد (`canEdit` لا يُفتح قبل اعتماد الرد).
  - رفض رئيس الفرع نفسه: المعاملة مرفوضة كأي مستند، تُصحَّح وتُرسل من جديد.
- **الصلاحيات:** `ViewStudyDiwanTransactions`، `CreateStudyDiwanTransaction`، `EditStudyDiwanTransaction`، `DeleteStudyDiwanTransaction` (بدل `ManageStudyDiwanCorrespondence`)، والإلغاء `CancelStudyDocuments`. الحدّ فرع الدراسة (مدير النظام ونظرة المؤسسة كل الفروع).
- **الإلغاء:** لا تُلغى المعاملة ولها أمر بدء قائم، ولا يُلغى الرد أو يُحذف وله معاملة قائمة.
- **الترحيل** `Study_Diwan_Transactions`: كل معاملة قديمة صارت مستنداً (المرحلة 2 مسودة، 3 بانتظار الاعتماد، 4 مرفوضة، 5 و7 معتمدة، 6 مرفوضة بقرار رفض)، وكتب الرد مرفقات، وأوامر البدء المرتبطة بكتاب صارت مرتبطة بمعاملتها؛ الأوامر الأقدم بلا معاملة. الجدولان القديمان باسم `StudyDiwanCases_Legacy` و`StudyDiwanReplies_Legacy` حتى التحقق.

### الطباعة — `api/StudyPrint`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `Request/{id}`، `Assignment/{id}`، `Response/{id}`، `DiwanTransaction/{id}`، `StartOrder/{id}`، `DesignApproval/{id}` (CDA) | `PrintStudyDocuments` + صلاحية عرض المستند نفسه (ViewStudyRequests / ViewStudyAssignments / ViewStudyResponses / ViewStudyStartOrders / ViewStudyDrawings)، وحدّها فرع الدراسة |

يعيد `StudyPrintDto`:
- **الترويسة:** `title`، `number` (رقم الديوان المدخل يدوياً للطلب، وIPA-/RSP-/PIO-/CDA-yy-nnnn لبقية المستندات)، `branchName` (اسم فرع الدراسة من قاعدة البيانات)، `documentDate` و`documentDateHijri` (`1448/04/12`، بقاعدة طباعة الإجازة نفسها `VacationRules.HijriDayOffset`).
- **المستند:** كاملاً في خاصية واحدة مملوءة بحسب النوع: `request` أو `assignment` أو `response` أو `diwanTransaction` أو `startOrder` أو `designApproval`، وهي مخرجات الـ `Get` نفسها. تاريخ ترويسة معاملة الديوان = تاريخ الرد المركزي (أو الصادر قبله).
- **التواقيع (`signatures`):** سطر لكل صفة بالترتيب: `signerRoleName`، `signerName`، `jobTitle`، `decisionName`، `decidedAt` و`decidedAtHijri`، `note`، و`image`، وهي **نسخة التوقيع المحفوظة وقت القرار** (Data URL) لا التوقيع الحالي. تغيير التوقيع لاحقاً لا يغيّر المطبوع. `image = null` ترسم مربعاً فارغاً (قرار بلا توقيع، أو لم يُقرَّر بعد).
- **المضاف:**
  - الطلب: `request.requestNumber` و`request.urgencyName`.
  - الرد: `response.costItems` و`totalCost` للجدول التفصيلي ملحقاً.
  - أمر البدء: توقيع رئيس القسم المختص ضمن `signatures` (الصفة «رئيس القسم المختص») مع `startOrder.competentDepartmentName`.
  - CDA: `startOrderNumber`.

### المشاريع — `api/StudyProjects`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `GetAll`، `Get/{id}` (الأقسام والأعضاء والمراحل ونسب التقدم والتواقيع) | ViewStudyProjects |

- **`GetAll?siteId=`:** فلتر اختياري بموقع واحد من سجل المواقع (لعرض مشاريع الموقع عند النقر عليه في الخريطة). يتركّب مع بقية الفلاتر (الحالة، البحث، التاريخ) والترقيم كما هو، وحدّ فرع المستخدم باقٍ، فلا يرى أحد مشاريع موقع خارج فرعه. تتجاهله بقية المستندات.

| POST | `Create` بالجسم `{ startOrderId, name, plannedStartDate, plannedEndDate, code? }` (مشروع المركزية) | CreateStudyProject |
| POST | `CreateRegional` بالجسم `{ requestId, name, referralDate, plannedStartDate, plannedEndDate, managerUserId?, adminArchiveNumber? }` — الإحالة إلى الفرع الجغرافي؛ `referralDate` سنته في الكود وهو البدء الفعلي، `managerUserId` الافتراضي من يحيل (المدير العام بلا فرع يجب أن يختار موظفاً)، `adminArchiveNumber` إن خلا منه الطلب | CreateRegionalStudyProject |
| PUT | `Update/{id}` (`status`: 1 جارٍ أو 2 معلّق، و`managerUserId`) | EditStudyProject |
| DELETE | `Delete/{id}` (بلا مهام ولا مخططات ولا مرفقات) | DeleteStudyProject |
| PUT | `Cancel/{id}` | CancelStudyDocuments |
| PUT | `Reactivate/{id}` (يعيد المشروع الملغى إلى «جارٍ» إذا كان أمر البدء معتمداً — أو الطلب غير ملغى لمشروع فرع جغرافي — ويعيد تفاصيل المشروع) | CancelStudyDocuments |
| PUT | `RequestCompletion/{id}` (كل المراحل مغلقة؛ التواقيع حسب المسار) | EditStudyProject |
| POST / DELETE | `Departments/{projectId}` بالجسم `{ departmentId }` / `Departments/{projectDepartmentId}` | Add/RemoveStudyProjectDepartment |
| POST / DELETE | `Members/{projectDepartmentId}` بالجسم `{ userId, roleTitle }` / `Members/{projectDepartmentId}/{userId}` | Add/RemoveStudyProjectMember (رئيس القسم لموظفي قسمه؛ انظر `departments[].canManageMembers`) |
| GET | `ActivityLog/{id}` | ViewStudyActivityLog |

- **مكاتب الأقسام المشاركة:** كل `departments[]` في التفاصيل يحمل `offices: [{ id, name }]` (مرتبة بالاسم، وقائمة فارغة لقسم بلا مكاتب). منها يُختار هدف «مكتب» في المهمة، بلا `ViewOffices`.
- **`siteId`:** موقع المشروع في سجل المواقع، منسوخ من طلب الدراسة عند إنشاء المشروع (قد يكون فارغاً). منه تظهر المشاريع على خريطة الفرع (انظر «الخريطة» أدناه).
- **تفاصيل المشروع:** تأتي من `Get/{id}` ومن ردود كل التعديلات (الأقسام والأعضاء…)، أما `GetAll` فلا يحمل الأقسام.
- **`managerUserId`:** موظف نشط من فرع المشروع (من `StudyLookups/Employees`).

### حدّ رؤية المشروع بالقسم والمكتب (2026-11-10)

`ViewStudyProjects` وحدّ الفرع يبقيان بوابة الدخول. داخل مشاريع الفرع يتحدد ما يراه المستخدم من **ثلاث صلاحيات جديدة** تُمنح من صفحة الأدوار (لا يُستنتج المنصب من الدور ولا من الموقع):

| الصلاحية | ما يراه صاحبها |
|---|---|
| `ViewFullStudyProject` | المشروع كاملاً (السلوك السابق): كل المراحل والمهام والمخطط الزمني والفريق والمرفقات والتواقيع وسجل النشاط |
| `ViewDepartmentStudyTasks` | مراحل قسمه، وكل المهام فيها، والمسندة لقسمه أو مكاتبه أو موظفيه، وأعضاء فريق قسمه |
| `ViewOfficeStudyTasks` | المهام المسندة لمكتبه أو لموظفيه، وأعضاء فريق مكتبه |
| بلا أيٍّ منها | مهامه فقط (المسندة إليه أو إلى مكتبه، أو إلى قسمه إن لم يكن له مكتب) وزملاء مكتبه |

- **الأسبقية:** القسم يغني عن المكتب. وصلاحية القسم أو المكتب لمن لا قسم له (أو لا مكتب له) لا تعطي شيئاً فيبقى على «مهامي».
- **مدير المشروع** (`managerUserId`) ومدير النظام يريان المشروع كاملاً دائماً. حدّ سجل لا منصب.
- **Migration** `Study_Project_View_Scopes`: منحت `ViewFullStudyProject` للأدوار التي تملك `CreateStudyStage` أو `EditStudyProject` أو `CreateStudyProject` أو `SignStudyAsBranchHead` أو `SignStudyAsProjectManagement` كي لا يفقد أحد رؤيته. الصلاحيتان الأخريان تُمنحان يدوياً.
- **الأعلام في تفاصيل المشروع وقائمته:** `canViewFull` (bool)، و`viewLevel` (1 كاملة، 2 مهام القسم، 3 مهام المكتب، 4 مهامي) و`viewLevelName`. أخفِ في الواجهة ما لا يصل إليه المستخدم. الأعلام الحالية (`canEdit`…) باقية.

ما يُحدّ لغير صاحب الرؤية الكاملة:

| البيانات | القاعدة |
|---|---|
| رأس المشروع ونسبته الكلية | كما هو، والنسبة الكلية محسوبة من **كل** المراحل |
| المراحل (`Get` و`GetAll` و`StudyStages/ByProject`) | مراحل قسمه (`responsibleDepartmentId`) يراها كل موظفيه، وأي مرحلة فيها مهمة يراها. مراحل الوثائق لا تظهر |
| عدد المهام ونسبة المرحلة | كاملان في المرحلة المرئية (`tasksTotal`، `tasksDone`، `progress`) بلا تفاصيل مهام الزملاء |
| المهام (`ByStage`، `ByProject`، `Get/{id}`) | بالجدول أعلاه. `X-Total-Count` للمرئي فقط |
| الفريق (`departments` في التفاصيل) | صف قسمه وحده: بصلاحية القسم كل أعضائه، وبغيرها أعضاء مكتبه فقط (أو هو وحده إن لم يكن له مكتب). قسم غير مشارك: قائمة فارغة |
| التواقيع (إنجاز المشروع) | لمن وقّع سطراً أو يملك صلاحية توقيع سطر منها. وإلا `approvals: []` في التفاصيل و403 في `StudyApprovals/ByOwner` |
| المرفقات | مرفقات **المشروع نفسه**: لمن رفعها فقط (`ByOwner`)، و403 لتنزيل مرفق غيره. مرفقات الوثائق والمخططات والنسخ كما هي |
| سجل النشاط (المساران) | تُخفى أحداث المشروع والمراحل والأقسام المشاركة والفريق ومرفقات المشروع. تبقى مهام يراها فقط (لا المحذوفة) وأحداث الوثائق والمخططات |
| المخططات (`ViewStudyDrawings`) | لا تغيير: الكل يرى كل المخططات، والتعديل والحذف للقسم المُصدِر |

- **الطلب المباشر خارج الحدّ:** مهمة أو مرحلة أو مرفق بالمعرّف يعيد **403** لا قائمة فارغة.
- **لا يتغير:** `StudyTasks/My`، تحديث الإنجاز، الإشعارات، و`Pending` للتواقيع.
- **ردود التعديلات** (إضافة عضو، إعادة ترتيب المراحل…) تمرّ على الحدّ نفسه، فرئيس القسم الذي يضيف عضواً يتلقى المشروع بحدّ رؤيته.

### الخريطة

**صفحة الموقع (2026-11-09):** تُفتح بالنقر على موقع في خريطة الفرع، وتعرض كل بطاقة بصلاحيتها: بطاقة «مشاريع الدراسات» لمن يملك `ViewStudyProjects` (من `StudyProjects/GetAll?siteId=`)، والتركيبات لصلاحيات الأجهزة، ومعلومات الفروع لسجلات الطبقات. لذلك أُضيفت `ViewStudyProjects` و`CreateStudyRequest` و`EditStudyRequest` إلى سياسة `AnySiteView` (قراءة `Sites/Get` و`Sites/GetAll`): الأولى لفتح الصفحة، والأخريان لقائمة اختيار الموقع في نموذج الطلب.

مزوّد خريطة جديد `studies` (العنوان «الدراسات»، الوحدة «مشروع») يظهر تلقائياً في خريطة الفرع ونافذة «طبقات الفروع» (`MapLayers/Sources`)، ولا يحتاج تعديلاً في الواجهة:
- **المصدر:** مشاريع الفرع التي لها `siteId`، مع عدد المشاريع في كل موقع (`count`). تتبع تفاصيل الموقع صلاحيات المشاريع.
- **الطبقة التلقائية:** كل مشاريع الفرع **عدا الملغاة**. لا تظهر الملغاة إلا بطبقة تحدد حالتها صراحةً.
- **الفلاتر:** `status` (حالة المشروع: جارٍ، معلّق، مكتمل، ملغى) و`workType` من الطلب (1 إنشاء، 2 صيانة وترميم، 3 إنشاء جديد / صيانة وترميم). يطابق الفلتر القيم المختارة، ويُختار النوع المدمج صراحةً لإضافته إلى طبقة مفلترة.
- **ربط الطلب بموقع:** لا يظهر المشروع على الخريطة إلا إن اختير موقع في الطلب (`siteId` في `StudyRequests/Create|Update`). الموقع يُنسخ للمشروع عند إنشائه، وتغيير موقع الطلب بعد ذلك غير ممكن لأن الطلب المعتمد لا يُعدَّل.

### المراحل — `api/StudyStages`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `ByProject/{projectId}` | ViewStudyProjects |
| POST | `Create` بالجسم `{ projectId, stageTypeId, weight, … }` | CreateStudyStage |
| PUT | `Update/{id}` (`status`: 1 أو 2) | EditStudyStage |
| PUT | `Reorder/{projectId}` بالجسم `{ stageIds: [...] }` | EditStudyStage |
| DELETE | `Delete/{id}` | DeleteStudyStage |
| PUT | `Close/{id}`، `Reopen/{id}` | CloseStudyStage |

- الإغلاق بالترتيب، وبعد إنجاز كل مهام المرحلة.
- مراحل المستندات (`linkedDocument ≠ 0`) تُنشأ مغلقة ولا تُحذف ولا يُعاد فتحها.
- **`canAddTask` (2026-11-10):** علم على كل مرحلة في `ByProject` وفي ردود الإنشاء والتعديل والإغلاق وإعادة الفتح والترتيب، وفي `stages` داخل تفاصيل المشروع. يكون `true` لمن يملك `CreateStudyTask` وهو من القسم المسؤول عن المرحلة (أو مدير النظام)، والمشروع مفتوح والمرحلة غير مغلقة. أخفِ زر «إضافة مهمة» عند `false`. (المراحل لا تحمل `canEdit`؛ إنشاء المراحل وتوزيعها على الأقسام بـ `CreateStudyStage`/`EditStudyStage` كما هو.)

### المهام — `api/StudyTasks`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `ByStage/{stageId}` | ViewStudyProjects |
| GET | `ByProject/{projectId}`: كل مهام المشروع في طلب واحد (لمخطط Gantt) | ViewStudyProjects |
| GET | `Get/{id}` | من يرى المشاريع، أو المكلَّف |
| GET | `My?openOnly=true&page&pageSize` | UpdateStudyTaskProgress |
| POST | `Create` بالجسم `{ stageId, targetType, departmentId \| officeId \| assigneeUserId, title, weight, … }` | CreateStudyTask |
| PUT | `Update/{id}`، DELETE `Delete/{id}` | Edit/DeleteStudyTask |
| PUT | `Progress/{id}` بالجسم `{ progress, status }` | UpdateStudyTaskProgress (المكلَّف فقط؛ انظر `canUpdateProgress`) |

- **من يدير مهام المرحلة (2026-11-10):** إنشاء المهمة وتعديلها وحذفها لمن يملك الصلاحية **وهو من القسم المسؤول عن المرحلة** (`ResponsibleDepartmentId`) فقط، وإلا `403` بالرسالة «مهام هذه المرحلة يديرها القسم المسؤول عنها». مدير النظام مستثنى. المرحلة بلا قسم مسؤول (مراحل الوثائق وكل مرحلة لم يُعيَّن لها قسم) لا يديرها غيره. وهكذا يوزّع رئيس إدارة المشاريع المراحل على الأقسام ثم يقسّم كل رئيس قسم مرحلة قسمه إلى مهام، وتبقى لرئيس إدارة المشاريع مهام المراحل المسؤول عنها قسمه. الصلاحيات نفسها (`CreateStudyTask`/`EditStudyTask`/`DeleteStudyTask`) ولا ترحيل. الحدّ على **المرحلة** لا على الهدف: الهدف يبقى أي قسم مشارك أو مكتب منه أو عضو في الفريق.
  - **لا يتأثر:** `Progress/{id}` (يحدّثه المكلَّف كما كان).
  - **هدف الإسناد (2026-11-10):** للمرحلة ذات القسم المسؤول تُسنَد مهامها إلى قسمها فقط: `departmentId` = القسم المسؤول، أو `officeId` لمكتب منه، أو `assigneeUserId` لعضو في فريق المشروع قسمه في حسابه هو القسم المسؤول. غير ذلك `400` بالرسالة «تُسنَد مهام المرحلة إلى القسم المسؤول عنها (قسمه ومكاتبه وأعضائه) فقط» (في الإنشاء والتعديل). مدير النظام مستثنى، والمراحل بلا قسم مسؤول كما كانت. تعاون قسمين يكون بمرحلة لكل قسم.
  - **الأعلام:** `canEdit` في المهمة (للتعديل والحذف معاً) تشمل القاعدة، و`canAddTask` في المرحلة (انظر أعلاه).
- **الحد الأعلى:** `ByProject` يعيد 2000 مهمة كحد أقصى (بترتيبه المعتاد). الرد قائمة كما هو، والترويستان `X-Total-Count` (العدد الكلي) و`X-Truncated` (`true` إن قُطع الرد) تكشفان ذلك لتنبّه المستخدم أو تضيّق بمرحلة (`ByStage`).
- **`ByProject`:** له حدّ `StudyStages/ByProject` نفسه، وأعلام `canEdit` و`canUpdateProgress` نفسها في `ByStage`.
  - الترتيب بترتيب المرحلة (`sortOrder`)، ثم البدء المخطط (المهام بلا بدء مخطط في آخر مرحلتها)، ثم الرقم.
  - كل مهمة تحمل `stageId` و`stageName` و`projectId` و`projectCode`، والتواريخ المخططة والفعلية.

قيم `targetType`:

- **1 قسم مشارك:** يُملأ `departmentId`.
- **2 مكتب من قسم مشارك:** يُملأ `officeId`.
- **3 عضو في الفريق:** يُملأ `assigneeUserId`.

### المخططات — `api/StudyDrawings`، و`api/StudyDesignApprovals`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `StudyDrawings/ByProject/{projectId}?kind`، `StudyDrawings/Get/{id}` (مع `versions` للمعماري) | ViewStudyDrawings |
| POST/PUT/DELETE | `StudyDrawings/Create` (`kind`: 1 معماري … 5 تقني)، `Update/{id}`، `Delete/{id}` | Create/Edit/DeleteStudyDrawing (يعدّل القسم المُصدِر) |
| PUT | `StudyDrawings/Review/{id}` بالجسم `{ approve, note }` (غير المعماري بانتظار المراجعة) | ReviewStudyDrawing |
| POST/PUT/DELETE | `StudyDrawings/Versions/{drawingId}`، `StudyDrawings/Version/{id}` | Create/Edit/DeleteArchitecturalVersion |
| GET/POST/PUT/DELETE | `StudyDesignApprovals/Get/{id}`، `Create` بالجسم `{ versionId, cdaDate, … }`، `Update/{id}`، `Delete/{id}` | ViewStudyDrawings / Create/Edit/DeleteDesignApproval |

- غير المعماري يُرسل `basedOnVersionId`، وهي نسخة معمارية معتمدة من المشروع نفسه.
- **المكتب (`officeId` / `officeName` في الاستجابة فقط):** لم يعد حقلاً في طلب الإنشاء أو التعديل. يملؤه الخادم **عند الإنشاء فقط** من مكتب المُنشئ إن كان تابعاً للقسم المُصدِر، ويكون `null` لمن لا مكتب له (كرئيس القسم) أو لمنشئ من قسم آخر، ولا يُرفض الطلب. التعديل لا يقرؤه ولا يمسّه، فمن يعدّل لاحقاً لا يمسح المكتب ولا يستبدله. توثيقي: لا أثر له على الصلاحيات أو المراجعة أو الاعتماد أو الإشعارات. المخططات الموجودة تحتفظ بقيمها.
- إصدار CDA ينشئ سطور التوقيع الثلاثة فوراً.

### التواقيع — `api/StudyApprovals`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `Pending` | أي صلاحية `SignStudyAs…` |
| GET | `ByOwner?ownerType&ownerId` | أي صلاحية عرض للدراسات |
| PUT | `Decide/{id}` بالجسم `{ decision, note }` | الصلاحية تُفحص لصفة السطر |

- **`ownerType`:** 2 تكليف، 3 رد، 4 أمر بدء، 5 مشروع، 10 CDA، 14 معاملة ديوان.
- **`decision`:** 2 معتمد، 3 معتمد مع ملاحظات، 4 مرفوض. الملاحظة مطلوبة للأخيرين.

### المرفقات — `api/StudyAttachments`

| الطريقة | المسار | الصلاحية |
|---|---|---|
| GET | `ByOwner?ownerType&ownerId` | صلاحية عرض صاحبه |
| POST | `Upload?ownerType&ownerId&attachmentTypeId` — الجسم multipart فيه حقل ملف واحد `file` | صلاحية تعديل صاحبه |
| GET | `Get/{id}?download=true` (الصور وPDF تُعاين مباشرة؛ انظر `previewable`) | صلاحية عرض صاحبه |
| DELETE | `Delete/{id}` | صلاحية تعديل صاحبه |

- **`ownerType`:** 1 طلب، 2 تكليف، 3 رد، 4 أمر بدء، 5 مشروع، 8 مخطط، 9 نسخة معمارية، 14 معاملة ديوان.
- **ملفات المخططات بلا سقف للحجم** (جُرّب ملف 1GB). المعرّفات في عنوان الطلب لا في النموذج، كي تُفحص الصلاحية قبل استقبال الملف.
  ملف واحد لكل طلب رفع، و100 ملف لكل سجل. `size` بالبايت (رقم 64-بت).
- **في الواجهة:** ارفع بـ `FormData` فيه `file` فقط، وأظهر شريط التقدم (`reportProgress: true` في `HttpClient`) — رفع 1GB يأخذ وقتاً على الشبكة.
  التنزيل يحتاج ترويسة الدخول ويدعم `Range` (يُستأنف إن انقطع). للملفات الكبيرة اطلبه بـ `fetch` واكتبه مباشرة على القرص بدل تحميله في الذاكرة كـ Blob.
- **الصيغ المسموحة:** صور، PDF، Office، DWG/DXF، نصوص، أرشيف مضغوط. النوع يُكشف من محتوى الملف.
- **التخزين (2026-11-11):** ملفات على القرص دائماً، لا في القاعدة. المجلد الجذر هو `FileStorage:Root`، وإن لم يُضبط فمجلد **`wwwroot/upload` داخل مجلد مشروع الـ API** (يُنشأ تلقائياً عند الإقلاع). فيه مجلد لكل مشروع: `wwwroot/upload/studies/projects/{رقم المشروع}/{معرّف الملف}` لمرفقات المشروع ومخططاته ونسخها، و`wwwroot/upload/studies/documents/{رقم الطلب}/…` لمستندات الدراسة قبل مشروعها (طلب، تكليف، رد، أمر بدء). اسم الملف الأصلي في القاعدة فقط (`fileName`)، ولا أثر لذلك على الواجهة. المسار `/upload/…` لا يُقدَّم كملف ثابت (404)، والمرفقات تُقرأ فقط من `StudyAttachments/Get/{id}` بصلاحياتها.

### سجل الدراسة

`GET api/StudyActivityLogs/ByRequest/{requestId}?page&pageSize` و`GET api/StudyProjects/ActivityLog/{id}?page&pageSize` بصلاحية `ViewStudyActivityLog`. السجل مرتب الأحدث أولاً.

- **الرد مرقَّم** (تغيير عن الشكل السابق، وكان قائمة): `PagedResultDto` = `{ items, totalCount, page, pageSize, totalPages }`. الافتراضي `pageSize=50` والأقصى 100. صفحة بعد الأخيرة تعيد `items` فارغة.
- **الأحداث:** كل عنصر فيه `id`، `entityType`/`entityTypeName`، `action`/`actionName`، `title`، `details`، `userId`/`userName`، `createdAt` كما كان.

## الإشعارات (تصل عبر SignalR كالمعتاد)

| `type` | متى | `relatedEntityType` |
|---|---|---|
| `StudyApprovalRequested` | مستند بانتظار توقيع صفتك | `StudyAssignment` / `StudyResponse` / `StudyStartOrder` / `StudyDesignApproval` / `StudyProject` |
| `StudyDocumentDecided` | اعتُمد مستندك نهائياً أو رُفض | كما سبق |
| `StudyTaskAssigned` | مهمة جديدة لك أو لمكتبك أو قسمك | `StudyTask` |
| `StudyNextStepReady` (29) | الخطوة التالية في السلسلة: الطلب ← التكليف (أو، لطلب فرع جغرافي، ← الإحالة لحاملي `CreateRegionalStudyProject`)، التكليف ← الرد، الرد المعتمد ← إنشاء معاملة الديوان (أو تحديثها بعد رفض مركزي)، المعاملة المعتمدة بموافقة ← أمر البدء، أمر البدء المعتمد ← إنشاء المشروع. الرفض المركزي يُشعر موظفي القسم المتابع ومنشئ المعاملة (الرابط إلى الرد). | `StudyRequest` / `StudyAssignment` / `StudyResponse` / `StudyDiwanTransaction` / `StudyStartOrder` |

## الصلاحيات في الواجهة

- **الأسماء:** في `Application/Common/AppPermissions.cs`، قسم «فرع الدراسات المركزية» (62 صلاحية، منها `EditStudyResponsePlanning` و`PrintStudyDocuments` وصلاحيات الرؤية الثلاث `ViewFullStudyProject` و`ViewDepartmentStudyTasks` و`ViewOfficeStudyTasks`). انسخها إلى `core/constants/access.ts`.
- **صفحة الأدوار:** أضف لها قسماً جديداً «الدراسات» بمجموعات: القوائم، الطلبات، التكليفات، الردود، أوامر البدء، التواقيع، المشاريع والفريق، المراحل والمهام، المخططات وCDA، السجل.
- **صلاحيات «أيٌّ من»:** `AnyStudySign`، `AnyStudyView`، `AnyStudyTaskView`، `AnyStudyAttachmentWrite`، `AnyStudyEmployeePick`، `AnyStudyBranchPick`.
- **جديد 2026-11-28 (تُمنح من صفحة الأدوار، للمدير العام فقط افتراضياً):** `CreateRegionalStudyProject` (الإحالة إلى الفرع الجغرافي) و`SignStudyAsCoordination` (توقيع التنسيق والارتباط على إنجاز دراسات الأفرع الجغرافية؛ ضمن `AnyStudySign`). أضفهما إلى `access.ts` وصفحة الأدوار.
