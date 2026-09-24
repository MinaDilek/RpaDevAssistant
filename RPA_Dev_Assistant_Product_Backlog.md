# RPA Dev Assistant – Product Backlog & Implementation Checklist

> Bu belge, RPA Dev Assistant projesinde planlanan özellikleri takip etmek için hazırlanmıştır.
> Codex bu dosyayı repository içinde okuyabilir, mevcut implementasyonu inceleyebilir ve gerçekten tamamlanmış maddeleri `[x]` olarak işaretleyebilir.

## Codex için kullanım talimatı

Bu dosyayı proje backlog/checklist kaynağı olarak kullan.

Kurallar:

- Repository içindeki mevcut kodu, testleri, frontend/backend yapısını ve dokümantasyonu incele.
- Bir maddeyi yalnızca gerçekten implement edilmiş ve çalışır durumda olduğuna dair kod/test/entegrasyon kanıtı varsa `[x]` yap.
- Kısmen yapılmış maddeleri `[ ]` bırak ve sonuna `— Kısmi: ...` şeklinde kısa not ekle.
- Sadece dosya veya sınıf adı var diye bir özelliği tamamlanmış sayma.
- Mock/sample seviyesindeki özellikleri production-ready olarak işaretleme.
- Mevcut özellikleri yeniden implement etme.
- Her inceleme sonunda:
  - Tamamlanan maddeleri işaretle.
  - Kısmi maddelere kısa durum notu ekle.
  - Yeni tespit edilen teknik borç veya eksik özellikleri en alttaki `Yeni Tespitler` bölümüne ekle.
- Bu belgeyi güncellerken başlık yapısını koru.

---

## 1. Analyzer & Code Quality

- [x] Gerçek UiPath projelerinde validation ve false-positive audit
- [x] Rule set’in genişletilmesi
- [x] UiPath Windows / Windows-Legacy / Modern / Classic proje desteğinin sağlamlaştırılması — Merkezi runtime/design compatibility matrisi, analiz context'i, RPA016 davranışı, Rule Catalog metadata'sı ve odaklı testler tamamlandı.
- [x] Workflow complexity analizi
- [x] Nesting depth analizi
- [x] Büyük workflow tespiti
- [x] Hard-coded değer tespiti
- [x] Hard-coded URL / dosya yolu tespiti — RPA020 (dosya yolu) ve RPA035 (URL) kuralları tamamlandı.
- [x] Credential / secret güvenlik kontrolleri
- [x] Selector kalite analizi
- [x] `idx` ve kırılgan selector kontrolleri
- [x] Timeout / ContinueOnError kontrolleri
- [x] Retry Scope / Check App State kullanım analizi — RPA046, aynı workflow içinde en az iki uzun literal Delay ve UI activity bulunup Retry Scope/Check App State bulunmadığında tek workflow-level Suggestion üretir; sayaçları bulgu evidence'ında gösterir.
- [x] Exception handling kalite analizi
- [x] BusinessRuleException kullanım analizi
- [x] Logging kalite analizi
- [x] Argument naming convention kontrolleri
- [x] Variable naming convention kontrolleri
- [x] Kullanılmayan argument/variable tespiti — RPA033 stable scope üzerinden nested variable shadowing'i; RPA034 workflow içi referansları, static caller mapping'lerini ve runtime `ArgumentsVariable` sözleşmelerini fail-safe biçimde değerlendirir.
- [x] Invoke Workflow argument mapping kontrolleri — RPA043 bilinmeyen mapping key'lerini, RPA045 caller/callee direction uyuşmazlıklarını ve RPA044 serialized default değeri olmayan eksik In/InOut mapping'lerini statik çağrılarda doğrular; runtime dictionary ve dynamic invoke çağrıları fail-safe biçimde dışarıda bırakılır.
- [x] Skor sisteminin gerçek projelere göre kalibre edilmesi

## 2. Workflow Intelligence

- [x] Workflow detay ekranı
- [x] Activity hierarchy/tree görünümü
- [x] Workflow bazlı Findings ekranı
- [x] Workflow bazlı AI Review
- [x] Workflow invocation graph
- [x] Süreç ve PDD Analizi — Mevcut proje analizini yeniden parse etmeden kullanan ayrı TR/EN ekran; süreç özeti, sistemler, akış, kanıta dayalı iş kuralları, salt-okunur TXT/Markdown PDD karşılaştırması ve gap analizi destekleniyor.
- [x] Project Architecture görselleştirmesi
- [x] Caller / Callee analizi
- [x] Kullanılmayan workflow tespiti — RPA037 kuralı ile entry point erişilebilirlik analizi tamamlandı.
- [x] Circular workflow reference tespiti — RPA036 kuralı ile invocation graph DFS döngü tespiti tamamlandı.
- [x] REFramework detaylı uygunluk analizi — Canonical workflow kapsamı, root Main, Main State Machine, core workflow parse sağlığı, static graph erişilebilirliği, bozuk Invoke Workflow referansları ve Config workbook varlığı açıklanabilir Pass/Fail/Unknown checklist'iyle analiz edilir; dynamic invoke ilişkileri yanlış fail üretmez.
- [x] Queue kullanım analizi — RPA047, bilinen Orchestrator Queue activity’lerindeki literal QueueName değerlerini raporlar; Config, variable ve runtime expression değerlerini dışarıda bırakır.
- [x] Config kullanım analizi
- [x] Config-kod karşılaştırması ile Config’de tanımlı ancak kodda kullanılmayan key/değerlerin tespiti
- [x] Kullanılmayan Config key/değerlerini temizleyerek yeni Config dosyası üretme
- [x] Kodda referans verilen ancak Config dosyasında bulunmayan Config key’lerinin tespiti
- [x] Kod içindeki hard-coded değerlerin Config’e taşınmaya uygunluk analizi — dinamik selector değerleri hariç
- [x] Hard-coded URL, dosya/klasör yolu, e-posta, Queue adı, Asset adı, timeout, retry count, environment ve API endpoint değerlerinin Config adayı olarak sınıflandırılması
- [x] Genel/sabit teknik değerlerin (`OK`, `True`, `1` vb.) yanlış pozitif üretmemesi için Safe Extraction Candidate filtresi
- [x] Config’e taşınacak hard-coded değerler için anlamlı Config key adı önerileri
- [x] Birden fazla workflow’da tekrar eden aynı hard-coded değerlerin tespiti ve Config’e taşıma önerisi
- [x] Config key kullanım/etki analizi: hangi workflow’larda ve kaç noktada kullanıldığının gösterilmesi
- [x] Config Usage Graph: Config key → kullanan workflow/activity ilişkilerinin görselleştirilmesi
- [x] Proje dışında bulunan `.xlsx` Config workbook'unun native dosya seçiciyle seçilip Analyze / Preview / Generate akışlarında kullanılabilmesi
- [x] Config değişiklikleri uygulanmadan önce Remove / Add / Change önizlemesi
- [x] Orijinal Config dosyasını değiştirmeden düzenlenmiş yeni Config dosyası üretme
- [x] Yeni Config dosyası için kullanıcı tarafından seçilebilir `Save As` indirme/kayıt yolu
- [x] Config dosyası düzenlenirken mevcut sheet/kolon yapısı ve mümkün olduğunca formatın korunması — Workbook kopyası üzerinde satır yapısı ve stiller korunuyor; eklenen satırlarda table, auto-filter, data validation ve named range kapsamları güvenli biçimde genişletiliyor. Merged cells, formüller, kolon ayarları ve workbook metadata odaklı regression testleriyle doğrulandı.
- [x] DEV / TEST / PROD gibi environment bazlı Config değerlerinin analiz edilmesi — Config Intelligence, DEV/TEST/UAT/STAGING/PROD sütunlarını karşılaştırır; eksik ve farklı değerleri salt-okunur raporlar, hassas key değerlerini API/UI katmanında maskeler.
- [x] Password, token, API key gibi hassas hard-coded değerler için Config yerine Orchestrator Asset / Credential önerisi
- [x] Flowchart tabanlı workflow’ları Sequence/standart workflow yapısına dönüştürme — Safe olarak sınıflanan root Flowchart workflow için controlled apply + backup + rollback; standalone XAML akışında ise orijinali değiştirmeden yeni Sequence dosyası olarak kaydetme destekleniyor.
- [x] Flowchart → Workflow dönüşüm önizlemesi — Workflow detail ve bağımsız Flowchart Converter sayfasında destekleniyor.
- [x] Flowchart dönüşümünde branch/decision mantığının korunması — FlowDecision, FlowSwitch ve shared continuation davranışı regression testleriyle korunuyor.
- [x] Flowchart içinde custom dependency aktivitelerinin tespiti, standart UiPath muadillerinin önerilmesi ve kullanıcı onayıyla XAML üzerinde otomatik değiştirilmesi — Analiz aşamasında custom namespace/aktivite tespiti, UI üzerinde standart UiPath aktivite öneri kartı/tablosu ve onaylı kaydetmede Sequence XAML içinde `ui:` namespace/aktivite dönüşümü tamamlandı.

## 3. Dependency & Package Analysis

- [x] Dependency/package analizi
- [x] Eski UiPath package tespiti — Offline legacy indicator korunuyor; UiPath resmî NuGet V3 metadata üzerinden deprecated version ve alternatif package bilgisi timeout/cache/fail-safe davranışıyla doğrulanıyor.
- [x] Uyumsuz package versiyonlarının tespiti
- [x] Kullanılmayan dependency tespiti
- [x] Modern / Classic activity uyumsuzluk analizi
- [x] Package version risk analizi — Declared-version alignment ile birlikte resmî UiPath/NuGet V3 metadata üzerinden latest stable version, deprecation ve vulnerability riskleri analiz ediliyor; metadata erişilemezse sonuç `Unknown` kalıyor.

## 4. AI Features

- [x] AI Project Review kalitesinin geliştirilmesi — Deterministic eval fixture'ları, structured response validation, güvenli fallback ve kanıta bağlı response mapping eklendi.
- [x] Workflow bazlı AI Review’un geliştirilmesi
- [x] Ask Project yeteneklerinin genişletilmesi
- [x] Evidence/citation sisteminin geliştirilmesi
- [x] AI cevaplarının güven skorunun geliştirilmesi
- [x] AI cevaplarında evidence ve interpretation ayrımının iyileştirilmesi — AI Review ve Ask Project contract'larında trusted evidence ile interpretation additive ve geriye uyumlu biçimde ayrıldı.
- [x] Türkçe AI Review desteği
- [x] Türkçe Ask Project desteğinin geliştirilmesi
- [x] Local LLM provider desteği — Review, Ask Project ve AI Fix için yalnız loopback adreslerine izin veren OpenAI Responses API uyumlu local provider desteği eklendi; redirect ile uzak endpoint’e çıkış engellenir.
- [x] AI provider seçimi — OpenAI/Local provider seçimi merkezi configuration üzerinden yapılır; üç AI akışı aynı provider ve model ayarlarını kullanır.

## 5. Fix & Refactoring

- [x] Fix Suggestion doğruluğunun artırılması
- [x] Patch Preview geliştirmeleri
- [x] Güvenli Apply Fix
- [x] Apply All Fix
- [x] Fix öncesi backup
- [x] Fix sonrası rollback
- [x] Project/file hash ile stale-change kontrolü
- [x] Local mutation/restore audit log — Apply, restore ve Flowchart dönüşüm işlemleri proje içindeki `.rpadevassistant/logs` altında append-only JSONL kayıtları ve operation/backup kimlikleriyle izlenir.
- [x] Safe Automatic Fix sınıflandırması
- [x] DisplayName otomatik düzeltme
- [x] Workflow rename preview — RPA006 önerisi eski/yeni relative path diff’ini, statik olarak çözümlenen caller workflow’ları ve güncellenmesi gereken Invoke Workflow File referanslarını gösterir; dynamic/external referansları açıkça manuel doğrulamaya bırakır ve dosya değiştirmez.
- [x] Workflow rename sırasında Invoke Workflow referanslarının güncellenmesi — RPA006 için kullanıcı onaylı rename transaction’ı, static root/caller-relative ve nested `WorkflowFileName` referanslarını XML-aware günceller; dynamic referansları raporlar, mandatory multi-file backup, stale hash, ordered locks, post-parse validation, audit ve tam rollback uygular.
- [x] Flowchart → Sequence dönüşüm preview — Workflow detail ve standalone XAML seçimi için doğrulandı.
- [x] Flowchart → Sequence kontrollü dönüşüm — Safe conversion apply, stale hash kontrolü, backup/rollback ve standalone Save As akışıyla doğrulandı.
- [x] Refactoring önerileri — RPA025 için parsed activity, nesting, decision, loop ve argument metriklerinden açıklanabilir decomposition planı üretilir; explicit argument contract, exception/retry sınırları ve adım adım regression doğrulaması önerilir, business boundary uydurulmaz ve auto-apply yapılmaz.

## 6. Localization & UX

- [x] Ortak uygulama shell’i ve ana çalışma ekranlarının kompakt desktop dashboard tasarım diline uyarlanması
- [x] Rules ekranının metrikler, filtreler, katalog tablosu, seçili rule detayı ve custom rule builder ile yoğun desktop katalog düzenine getirilmesi
- [x] Türkçe / İngilizce dil desteği
- [x] Bulguların Türkçeleştirilmesi
- [x] Recommendation metinlerinin Türkçeleştirilmesi
- [x] Fix Suggestion metinlerinin Türkçeleştirilmesi
- [x] AI Review çıktılarının seçili dile göre üretilmesi
- [x] Workflow sayısı kartının tıklanabilir olması
- [x] Finding sayısı kartlarının tıklanabilir olması
- [x] Workflow arama
- [x] Workflow filtreleme
- [x] Finding filtreleme
- [x] Finding arama
- [x] Finding severity sıralaması — Findings ekranında varsayılan sıra ile yüksekten düşüğe/düşükten yükseğe severity sıralama kontrolü eklendi.
- [x] Workflow risk göstergesi
- [x] Activity tree içerisinde arama — Workflow Detail içinde ad, DisplayName, tür ve property değerleri aranır; eşleşen activity’lerin parent hierarchy’si korunur, sonuç sayısı ve temizleme/boş durumları TR/EN destekler.
- [x] Büyük projelerde performans iyileştirmeleri — Workflow/finding türetilmiş verileri cache'lendi, workflow finding sayımı tek geçişe indirildi, Findings kademeli ve Activity Tree açılan dal bazlı render ediliyor. Backend artık workflow keşfi, XAML parse, scan, rule analysis, scoring ve toplam süreleri raporluyor; 51 workflow/6.228 activity içeren gerçek E-Haciz proje kopyasında 5 sıcak koşunun medyan toplam süresi 307,7 ms olarak ölçüldü.

## 7. Reports

- [x] HTML rapor geliştirmeleri
- [x] PDF rapor export
- [x] Yönetici özeti — Canonical rapor modelinde risk düzeyi, Critical/Error toplamı, etkilenen workflow sayısı, en çok etkilenen workflow ve öncelikli Rule ID’ler üretilir; HTML/PDF çıktılarında TR/EN narrative yönetici özeti gösterilir.
- [x] Teknik detay raporu
- [x] Workflow bazlı rapor
- [x] Rule bazlı rapor
- [x] Şirket standardı compliance raporu — Seçili aktif rule profile üzerinden uyum oranı ve ihlal listesi üretilir; bunun resmi denetim sertifikasyonu olmadığı raporda açıkça belirtilir.
- [x] Önceki analizle karşılaştırmalı rapor — Mevcut history snapshot’ı varsa skor, bulgu ve workflow değişimleri opsiyonel rapor bölümü olarak üretilir.
- [x] AI Review’un rapora eklenmesi — Yalnız explicit `includeAiReview` seçimiyle ve provider yapılandırılmışsa minimize/redacted evidence tabanlı AI Review rapora eklenir.
- [x] Fix önerilerinin rapora eklenmesi — Deterministic fix suggestion’lar opsiyonel ve read-only rapor bölümü olarak eklenebilir; mutation tetiklenmez.
- [x] Report branding / şirket logosu — Şirket adı, vurgu rengi ve güvenli PNG/JPEG/WebP data-URI logo yapılandırması HTML raporda desteklenir; text-only PDF şirket adını taşır.

## 8. Rule Profiles

- [x] Custom Rule Profiles
- [x] Local rule/profile yönetimi — Single-user desktop kullanımında custom rule/profile oluşturma, proje-profile eşleme, import/export ve default profile overlay desteklenir.
- [x] Şirket standartlarına özel rule set
- [x] Strict profile
- [x] Legacy profile
- [x] REFramework profile
- [x] Modern UiPath profile
- [x] Migration profile
- [x] Kullanıcı tarafından rule enable/disable
- [x] Rule severity özelleştirme
- [x] Rule weight özelleştirme
- [x] Rule max penalty özelleştirme
- [x] Şirket bazlı custom rule ekleme

## 9. Analysis History

- [x] Analysis history
- [x] Önceki analizle karşılaştırma
- [x] Skor değişim geçmişi
- [x] Yeni finding tespiti
- [x] Kapanan finding tespiti
- [x] Değişmeyen finding tespiti
- [x] Workflow bazlı değişim geçmişi
- [x] Trend analizi — Analysis History ekranında proje bazlı son 12 snapshot için skor ve bulgu sayısı trend grafiği, erişilebilir veri noktaları ve tarih aralığı gösterilir.

## 10. UiPath Integration

- [x] UiPath Studio entegrasyonu — Desktop Workflow Detail seçili project-relative `.xaml` dosyasını canonical path/proje sınırı kontrolleriyle Studio file association üzerinden açar; ters yönde External Tool başlangıç context’ini destekler.
- [x] UiPath Studio’dan RPA Dev Assistant açma — Paketlenmiş executable `--project <root> --workflow <relative.xaml>` argümanlarını kabul eder ve UiPath Studio External Tool tanımından güvenli biçimde başlatılabilir.
- [x] UiPath Studio’da seçili workflow’u analiz etme — Startup context normal production proje analizini çalıştırır, separator/case normalize ederek seçili workflow’u bulur ve Workflow Detail’i otomatik açar.
- [x] Orchestrator entegrasyonu — Backend-only credential ile salt-okunur inventory endpoint’i ve TR/EN masaüstü ekranı; yapılandırılmamış durumda fake veri üretmez.
- [x] Orchestrator process analizi — Release adı, process key, version ve latest-version metadata’sı resmi OData response’undan listelenir.
- [x] Queue analizi — Queue definition adı, açıklaması ve maksimum retry metadata’sı salt-okunur analiz edilir.
- [x] Asset analizi — Asset adı, scope ve type listelenir; secret/value içeriği istenmez veya frontend’e gönderilmez.
- [x] Robot / Machine konfigürasyon analizi — Machine adı ve type metadata’sı salt-okunur inventory’de gösterilir.
- [x] Orchestrator API entegrasyonu — HTTPS base URL, Bearer token, tenant/folder header’ları, timeout ve güvenli provider hata contract’ı desteklenir.
- [x] Automation Suite desteği — Aynı yapılandırılabilir HTTPS Orchestrator OData contract’ı `DeploymentType=AutomationSuite` ile on-prem Suite endpoint’lerinde kullanılabilir.

## 11. Git & CI/CD

- [x] GitHub entegrasyonu — Backend-only token ile Pull Request metadata’sı okunur; base/head commit’leri local salt-okunur Git comparison motorunda analiz edilir.
- [x] GitLab entegrasyonu — Merge Request metadata ve note endpoint’leri desteklenir; token frontend’e taşınmaz.
- [x] Azure DevOps entegrasyonu — Proje base URL’i ve PAT ile Pull Request metadata/thread comment contract’ı desteklenir.
- [x] Pull Request code review — Provider PR commit’leri mevcut production analyzer/profile/scoring hattıyla karşılaştırılır ve sonuç İnceleme Geçmişi ekranında gösterilir.
- [x] Commit bazlı analiz — İki local Git ref’i `git archive` ile izole temp snapshot’lara çıkarılır, mevcut production analyzer ile analiz edilir; skor, finding ve değişen dosya farkları çalışma ağacına dokunmadan gösterilir.
- [x] Branch karşılaştırması — Branch/tag/commit ref’leri aynı salt-okunur karşılaştırma contract’ı ve İnceleme Geçmişi UI’ı üzerinden karşılaştırılır; unsafe ref’ler reddedilir.
- [x] CI/CD quality gate
- [x] Minimum score threshold
- [x] Error/Critical finding varsa pipeline fail
- [x] Review sonuçlarını PR comment olarak yazma — Quality score ve finding fark özeti yalnız explicit kullanıcı aksiyonu sonrası GitHub comment, GitLab note veya Azure DevOps thread olarak yayınlanır.

## 12. Company Internal Version

- [ ] Şirket içi merkezi kullanım
- [ ] Kullanıcı yönetimi
- [ ] Rol ve yetki yönetimi
- [ ] SSO
- [ ] Merkezi/kurumsal audit log
- [ ] Merkezi çok kullanıcılı rule/profile governance
- [ ] Merkezi analysis history
- [ ] Takım bazlı projeler
- [ ] Şirket dashboard’u
- [ ] Proje kalite trendleri
- [ ] Developer/team bazlı kalite metrikleri
- [ ] On-premise deployment

## 13. Enterprise / Customer Version

- [ ] Multi-tenant yapı
- [ ] Müşteri bazlı izolasyon
- [ ] Müşteri bazlı rule profile
- [ ] Müşteri bazlı branding
- [ ] Lisanslama
- [ ] Subscription modeli
- [ ] Kullanım kotası
- [ ] Enterprise SSO
- [ ] Audit / compliance
- [ ] Customer admin panel
- [ ] On-premise seçeneği
- [ ] SaaS seçeneği
- [x] Private AI provider desteği

## 14. Product Platform

- [ ] Auto-update — Kısmi: Tauri v2 signed updater, HTTPS GitHub Releases endpoint, desktop-only background check, explicit install/relaunch UI, TR/EN states, protected signing key ve tag release manifest workflow’u hazır. İlk imzalı `v*` release yayınlanıp installed-app upgrade doğrulanmadan tamamlandı sayılmayacak.
- [x] Telemetry / kullanım istatistikleri
- [x] Crash reporting
- [x] Plugin/modül sistemi — Versioned/schema-validated deklaratif JSON modülleri custom rule ve custom profile’ları birlikte taşır; import tamamı doğrulanmadan başlamaz, built-in `RPA*` kimlikleri korunur ve executable plugin code kabul edilmez.
- [x] Feature flag sistemi
- [x] Configuration management — Local desktop runtime için CORS allowlist, custom rule/profile storage, analysis history retention/root ve resmî package metadata endpoint/cache ayarları tek typed ve startup’ta doğrulanan configuration contract’ında merkezileştirildi; environment override ve güvenli varsayılanlar desteklenir, secret değerler loglanmaz.
- [x] Desktop installer iyileştirmeleri — Windows 2022 CI üzerinde backend/frontend validation, win-x64 sidecar publish, Tauri NSIS build, non-empty artifact doğrulama, artifact retention ve sertifika secret’ları sağlandığında timestamp’li installer signing akışı eklendi; stabil bundle identifier upgrade continuity’sini korur.
- [ ] Code signing
- [x] Windows installer — Windows 2022 CI üzerinde 510 backend ve 126 frontend testinden sonra NSIS installer üretimi, sessiz kurulum, desktop executable ve bundled .NET sidecar doğrulaması tamamlandı (`Windows Desktop Release` run `35317709978`); indirilen 0.1.0 x64 installer SHA-256 değeri `8c238af8725ce4502f1230f0be73360c80f664f1445dc71d03c3075eabfa0999`.
- [x] macOS desteği — macOS üzerinde Tauri desktop uygulaması, backend sidecar ve dev ortamı çalışır durumda doğrulandı.
- [x] Linux desteği — Ubuntu 22.04 GitHub Actions run `36051909068` üzerinde backend/frontend doğrulaması, `linux-x64` self-contained sidecar ve Tauri `.deb`/`.AppImage` paket üretimi başarıyla tamamlandı. Doğrulanan 0.1.0 paket SHA-256 değerleri: `.deb` `48fc63a1dc211fe653a2137851ff3843a6c90ee7f3ea75eb599f24d4944ff251`, AppImage `e124898d8738225f9d3dd7e6d21ae7da315c19b81d2ad1ade223c6325a4c7c44`.

## 15. Variable & Argument Intelligence

- [x] XAML parser'da workflow argument adı, type ve direction bilgisinin çıkarılması
- [x] XAML parser'da variable adı, type, default value ve scope bilgisinin çıkarılması
- [x] Static Invoke Workflow File argument mapping key/value bilgilerinin parser modeline aktarılması
- [x] Argument/variable kullanımlarını expression'lardan güvenilir ve case-insensitive çözümleme — Wrapper/member expression token çözümlemesi ve stable scope `IdRef` üzerinden nested variable shadowing analizi tamamlandı; locator bulunmadığında fail-safe davranır.
- [x] Naming convention kurallarının profile/proje bazlı yapılandırılabilmesi — RPA006 workflow pattern/prefix, RPA031 In/Out/InOut prefix ve RPA032 variable pattern/prefix ayarlarını seçili rule profile’dan okur; proje-profile eşlemesiyle proje bazında uygulanır ve geçersiz regex güvenli varsayılana döner.
- [x] Argument direction kontrolü (In / Out / InOut) — RPA038 parsed expression access'ini read/write olarak sınıflandırır; yazılan In ve yalnız okunan Out argument'leri workflow-level finding olarak raporlar.
- [x] Gereksiz InOut argument tespiti — RPA039 yalnız okunan InOut için In, yalnız yazılan InOut için Out direction önerir; read/write ve kullanılmayan argument'lerde duplicate finding üretmez.
- [x] Argument type kontrolü — RPA040 eksik veya hatalı `InArgument(...)`, `OutArgument(...)` ve `InOutArgument(...)` type declaration'larını workflow-level olarak raporlar; nested generic type ifadelerini destekler.
- [x] Argument default value kontrolü — RPA048, Out argument default’larını, explicit boş default’ları ve nullable olmayan value type’lar için null default değerlerini deterministik olarak raporlar.
- [x] Argument naming convention kontrolü
- [x] Variable naming convention kontrolü
- [x] Kullanılmayan variable tespiti — RPA033 doğrudan expression referanslarını ve stable scope `IdRef` bulunan nested/shadowed variable kullanımlarını case-insensitive çözümler; belirsiz scope'larda false-positive üretmez.
- [x] Kullanılmayan argument tespiti — RPA034 doğrudan expression referanslarını, case-insensitive static Invoke mapping key'lerini, caller-relative path'leri ve runtime `ArgumentsVariable` sözleşmelerini denetler; çözümlenemeyen dinamik çağrılarda false-positive üretmez.
- [x] Gereğinden geniş variable scope tespiti — RPA041 yalnız stable activity ancestry ile doğrulanan ve tüm referansları daha dar bir Sequence, Flowchart veya StateMachine içinde kalan variable'ları raporlar.
- [x] Aynı isimli shadowed variable tespiti — RPA042 yalnız ancestor/descendant scope ilişkisi kesin olan case-insensitive aynı adlı nested variable declaration'larını raporlar; sibling ve locator'sız scope'larda fail-safe davranır.
- [x] Invoke Workflow argument mapping doğrulaması — RPA043 yalnız statik ve tekil olarak çözümlenebilen callee workflow contract'ında bulunmayan mapping key'lerini raporlar.
- [x] Eksik Invoke Workflow argument mapping tespiti — RPA044 map edilmemiş, type declaration ile yönü doğrulanmış ve serialized default değeri bulunmayan In/InOut argument'leri Suggestion olarak gösterir; default'lu input'ları, Out argument'leri, runtime dictionary mapping'lerini ve dynamic invoke çağrılarını dışarıda bırakır.
- [x] Yanlış direction ile map edilen argument tespiti — Parser mapping wrapper direction bilgisini korur; RPA045 bunu hedef workflow declaration direction'ı ile karşılaştırır.
- [x] Workflow argument contract görünümü — Workflow Detail, In/Out/InOut özetlerini ve argument adı, yönü, sözleşme rolü ile tipini TR/EN gösterir; kanıtlanamayan zorunluluk/default bilgisini tahmin etmez.

---

## Yeni Tespitler

> Codex repository incelemesi sırasında bu listede olmayan fakat eklenmesi gereken teknik borçları veya ürün özelliklerini buraya ekleyebilir.

- [x] Repo kökünde kararlı `PRODUCT_BACKLOG.md` giriş noktası oluşturuldu; tek kaynak yaklaşımını korumak için kanonik `RPA_Dev_Assistant_Product_Backlog.md` dosyasına yönlendirir.
- [x] Frontend tek dosyada büyümüş durumda; dashboard/settings/workflows/fix/AI ekranları component dosyalarına ayrılmalı — `main.tsx` 13 bağımsız bileşene (`components/`) ve modüler CSS dosyalarına ayrıldı.
- [x] AI Review ve Ask Project cevap dili seçili locale’a göre garanti edilmiyor; prompt/request seviyesinde locale bağlandı ve TR/EN acceptance ile doğrulandı.
- [x] RPA015 gerçek projede ambiguous finding üretiyor; default profile’da kapatıldı, rule catalog’da opsiyonel/custom profile ile açılabilir halde bırakıldı.
- [x] Findings ekranında ayrı RuleId/rule dropdown filtresi eklendi; RuleId filtresi category/severity/search ile birlikte regression testleriyle doğrulandı.
- [x] Backend-generated Fix Suggestion title/description/steps/risks için locale-aware contract yok; backend fix suggestion metinleri TR/EN locale ile doğrulandı.
- [x] Aktif Rule’lar ekranındaki şablon filtresi kaldırıldı; Profile Şablonu dropdown’ı aktif rule ekranına eklendi ve kayıt sonrası popup bağımsız toast state’iyle görünür kaldı.
- [x] Flowchart dönüşümü sırasında CommentOut, Comment ve XML yorum bloklarının dönüştürülen Sequence'e taşınması engellendi; Step 3 Preview & Save alanı görsel akış kartları, kullanılacak aktiviteler tablosu ve formatlanmış XAML kod önizleme bileşenleriyle zenginleştirildi.
- [x] Flowchart içinde custom dependency aktivitelerinin tespiti, standart UiPath aktivite önerileri ve kullanıcı onayıyla XAML üzerinde otomatik değiştirilmesi özelliği eklendi.
- [x] Flowchart analizinde ve dönüşümünde özel/transitif bağımlılıkların (ör. Ucgen.CustomLibrary altından gelen UiPath.WebAPI.Activities) tespiti yapıldı; HttpClient aktivitelerinin XML elemanları (EndPoint, Method, Result, StatusCode, Headers, Parameters, Attachments, Cookies) ve namespace tanımları eksiksiz korunacak şekilde XAML çevirici güncellendi, iç container'ların (Dictionary vb.) aktivite ismi sanılması engellendi ve eksik doğrudan paket bağımlılıklarının project.json'a eklenmesi sağlandı.
- [x] Flowchart XML `<Flowchart.StartNode>` alt eleman ve attribute çözümlemesi düzeltildi; başlangıç aktivitelerinin (ör. `retry_count = 1`) Sequence'in en başına yerleştirilmesi sağlandı.
- [x] Flowchart değişkenleri (`<Flowchart.Variables>`), üretilen Sequence köküne (`<Sequence.Variables>`) aktarılarak dönüştürülen akışta değişken tanımlarının (`retry_count`, `json_sorgu`, `result_sorgu`, `Durum`, `get_status` vb.) eksiksiz tanımlanması sağlandı.
- [x] CommentOut ve yorum bloklarının dönüştürülen Sequence içine taşınması XAML klonlama ve Sequence oluşturma aşamalarında derinlemesine temizlenerek tamamen engellendi.
- [x] `DeserializeJson`, `HttpClient` gibi aktiviteler için `UiPath.WebAPI.Activities` doğrudan bağımlılığı `project.json` dosyasına eklendi ve `njl:JObject` namespace çözümlemeleri korundu.

---

## İnceleme Özeti

**Son inceleme tarihi:** 2026-09-18
**İnceleyen:** Codex
**Tamamlanan madde sayısı:** 164
**Kısmi madde sayısı:** 0
**Bekleyen madde sayısı:** 60

### Notlar

- `PRODUCT_BACKLOG.md` dosyası bulunmadığı için bu inceleme repository’deki mevcut backlog kaynağı olan `RPA_Dev_Assistant_Product_Backlog.md` üzerinde yapıldı.
- Tamamlandı işaretleri kod, test, endpoint, frontend entegrasyonu ve README kanıtlarıyla sınırlı tutuldu.
