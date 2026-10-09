using PVZ.Core;

namespace PVZ.Catalog;

/// <summary>
/// Готовые сборники заданий по английскому языку. Каждый «_» в тексте — один пропуск;
/// количество ответов обязано совпадать с количеством пропусков (проверяется при сборке).
/// Варианты ответа разделяются «|», например «does not|doesn't».
/// </summary>
public static class EnglishCollections
{
    public static IReadOnlyList<FillBlankCollectionSeed> Build() => new List<FillBlankCollectionSeed>
    {
        new(3, "english-03-to-be.pvz", "To be: am / is / are", new[]
        {
            new FillBlankTaskSeed(
                "My name _ Tom. I _ nine. My friend Ann _ ten. We _ pupils. Her cat _ black. The cats _ funny.",
                new[] { "is", "am", "is", "are", "is", "are" }),
            new FillBlankTaskSeed(
                "It _ a ball. The ball _ red. You _ my friend. Anna and Tom _ at school. I _ happy. They _ from London.",
                new[] { "is", "is", "are", "are", "am", "are" }),
        }),
        new(3, "english-03-plurals.pvz", "Множественное число", new[]
        {
            new FillBlankTaskSeed(
                "One box — two _. One cat — three _. One bus — five _. One ball — six _. One fox — four _. One dog — two _.",
                new[] { "boxes", "cats", "buses", "balls", "foxes", "dogs" }),
            new FillBlankTaskSeed(
                "A child — many _. A mouse — two _. A man — three _. A woman — four _. A fish — ten _. A sheep — seven _.",
                new[] { "children", "mice", "men", "women", "fish", "sheep" }),
        }),
        new(4, "english-04-present-simple.pvz", "Present Simple: утверждения", new[]
        {
            new FillBlankTaskSeed(
                "Ann _ (like) ice cream. She _ (go) to school. Tom _ (play) football. He _ (watch) TV. My sister _ (read) books. The dog _ (sleep) in the hall.",
                new[] { "likes", "goes", "plays", "watches", "reads", "sleeps" }),
            new FillBlankTaskSeed(
                "I _ (get up) at seven. We _ (have) breakfast. They _ (walk) to school. You _ (do) your homework. The boys _ (run) in the park. Kate _ (dance) well.",
                new[] { "get up", "have", "walk", "do", "run", "dances" }),
        }),
        new(4, "english-04-have-got.pvz", "Have got / has got", new[]
        {
            new FillBlankTaskSeed(
                "I _ got a bike. Tom _ got a dog. We _ got two cats. She _ got a doll. They _ got a ball. My granny _ got a garden.",
                new[] { "have", "has", "have", "has", "have", "has" }),
            new FillBlankTaskSeed(
                "I _ got a computer. (not) She _ got a brother. (not) We _ got a garden. (not) Tom _ got a bike. (not) They _ got a map. (not)",
                new[] { "have not|haven't", "has not|hasn't", "have not|haven't", "has not|hasn't", "have not|haven't" }),
        }),
        new(5, "english-05-present-simple-questions.pvz", "Present Simple: вопросы и краткие ответы", new[]
        {
            new FillBlankTaskSeed(
                "Where _ you live? What _ she like? _ Tom play chess? When _ they come home? _ Ann and Kate go to school? Who _ he help?",
                new[] { "do", "does", "does", "do", "do", "does" }),
            new FillBlankTaskSeed(
                "Do you like apples? — Yes, I _. Does he swim? — No, he _. Do they read? — Yes, they _. Does she sing? — No, she _. Do we go? — Yes, we _. Does it fly? — No, it _.",
                new[] { "do", "does not|doesn't", "do", "does not|doesn't", "do", "does not|doesn't" }),
        }),
        new(5, "english-05-there-is-are.pvz", "There is / there are, some / any", new[]
        {
            new FillBlankTaskSeed(
                "There _ a book on the desk. There _ three cats in the yard. There _ a park near my house. There _ many trees in the garden. There _ a pen and two pencils in the bag. There _ no milk in the fridge.",
                new[] { "is", "are", "is", "are", "is", "is" }),
            new FillBlankTaskSeed(
                "I have got _ apples. (some) He hasn't got _ friends. (any) Are there _ eggs? (any) There are _ nice flowers. (some) I don't have _ questions. (any) She has got _ cheese. (some)",
                new[] { "some", "any", "any", "some", "any", "some" }),
        }),
        new(6, "english-06-past-simple.pvz", "Past Simple: правильные и неправильные глаголы", new[]
        {
            new FillBlankTaskSeed(
                "Yesterday I _ (go) to the park. We _ (play) football. Tom _ (see) a big dog. She _ (buy) a new dress. They _ (come) home late. My mum _ (make) a cake.",
                new[] { "went", "played", "saw", "bought", "came", "made" }),
            new FillBlankTaskSeed(
                "I _ (not / like) soup when I was little. She _ (not / go) to school yesterday. We _ (not / see) that film. He _ (not / come) to the party. They _ (not / eat) pizza. It _ (not / rain) in the morning.",
                new[] { "did not like|didn't like", "did not go|didn't go", "did not see|didn't see", "did not come|didn't come", "did not eat|didn't eat", "did not rain|didn't rain" }),
        }),
        new(6, "english-06-present-continuous.pvz", "Present Continuous: сейчас, в данный момент", new[]
        {
            new FillBlankTaskSeed(
                "I _ (read) a book now. She _ (swim) in the pool. They _ (watch) TV. We _ (do) our homework. Tom _ (run) in the garden. The cat _ (sleep) on the sofa.",
                new[] { "am reading", "is swimming", "are watching", "are doing", "is running", "is sleeping" }),
            new FillBlankTaskSeed(
                "Look! The children _ (play). Listen! Ann _ (sing). Be quiet! The baby _ (sleep). Hurry! The bus _ (come). Look! It _ (rain). Listen! The dogs _ (bark).",
                new[] { "are playing", "is singing", "is sleeping", "is coming", "is raining", "are barking" }),
        }),
        new(7, "english-07-comparatives.pvz", "Степени сравнения прилагательных", new[]
        {
            new FillBlankTaskSeed(
                "Tom is _ (tall) than Mike. Ann is the _ (good) pupil in the class. This book is _ (interesting) than that one. Winter is _ (cold) than autumn. My bag is _ (heavy) than yours. Everest is the _ (high) mountain.",
                new[] { "taller", "best", "more interesting", "colder", "heavier", "highest" }),
            new FillBlankTaskSeed(
                "A bus is _ (slow) than a train. This is the _ (bad) day. She is _ (happy) than her sister. These shoes are _ (expensive) than those. February is _ (short) than March. It is the _ (funny) story.",
                new[] { "slower", "worst", "happier", "more expensive", "shorter", "funniest" }),
        }),
        new(7, "english-07-quantifiers.pvz", "Many / much / a lot of / some / any", new[]
        {
            new FillBlankTaskSeed(
                "How _ apples do you have? How _ milk is there? There are _ books on the desk. (a lot of) I don't have _ money. There isn't _ sugar in the tea. How _ students are in your class?",
                new[] { "many", "much", "a lot of|lots of", "much", "much", "many" }),
            new FillBlankTaskSeed(
                "There is _ bread on the table. (some) Is there _ coffee? (any) There are _ people in the park. (a lot of) I have got _ questions. (some) We don't need _ help. (any) How _ water do you drink?",
                new[] { "some", "any", "a lot of|lots of", "some", "any", "much" }),
        }),
        new(8, "english-08-present-perfect.pvz", "Present Perfect: результаты и опыт", new[]
        {
            new FillBlankTaskSeed(
                "She _ (write) three letters today. They _ (buy) a new car. I _ (know) him since 2020. He _ (not / do) his task yet. We _ (be) friends for five years. Ann _ (just / come) home.",
                new[] { "has written", "have bought", "have known", "has not done|hasn't done", "have been", "has just come" }),
        }),
        new(8, "english-08-conditionals.pvz", "Условные предложения 1 типа (if … will)", new[]
        {
            new FillBlankTaskSeed(
                "If it _ (rain), we will stay at home. We will walk if the weather _ (be) fine. If you _ (help) me, I will finish early. She will call you if she _ (come) home. If we miss the bus, we _ (walk). I will read the book if I _ (have) time.",
                new[] { "rains", "is", "help", "comes", "will walk", "have" }),
        }),
        new(9, "english-09-passive-voice.pvz", "Passive Voice: страдательный залог", new[]
        {
            new FillBlankTaskSeed(
                "The letter _ (write) yesterday. Flowers _ (water) every day. The car _ (repair) now. The book _ (publish) next year. English _ (speak) all over the world. The dinner _ (cook) by my mum yesterday.",
                new[] { "was written", "are watered", "is being repaired", "will be published", "is spoken", "was cooked" }),
        }),
        new(9, "english-09-past-perfect.pvz", "Past Perfect: действие до прошлого", new[]
        {
            new FillBlankTaskSeed(
                "When we arrived, the film _ (start). She _ (finish) her work before lunch. They _ (leave) before the rain began. I realized that I _ (lose) my keys. By 2019 he _ (write) three books. The train _ (go) when we came.",
                new[] { "had started", "had finished", "had left", "had lost", "had written", "had gone" }),
        }),
        new(10, "english-10-modals.pvz", "Модальные глаголы: must / can / may / should", new[]
        {
            new FillBlankTaskSeed(
                "You _ (not / must) run in the corridor. She is very small, but she _ (can) swim well. _ I open the window? (may) We _ (have to) wear a uniform at school. He _ (not / be able to) come yesterday. You _ (should / not) eat so much sugar.",
                new[] { "must not|mustn't", "can", "May", "have to", "was not able to|wasn't able to", "should not|shouldn't" }),
        }),
        new(10, "english-10-conditionals-2.pvz", "Условные предложения 2 типа (would)", new[]
        {
            new FillBlankTaskSeed(
                "If I _ (be) a millionaire, I would travel the world. If she _ (know) the answer, she would tell us. We would go to the park if it _ (not / rain). If I had more time, I _ (learn) Chinese. He would help you if he _ (can). I would buy this dress if it _ (be) cheaper.",
                new[] { "were", "knew", "did not rain|didn't rain", "would learn", "could", "were" }),
        }),
        new(11, "english-11-tenses-review.pvz", "Все времена: повторение", new[]
        {
            new FillBlankTaskSeed(
                "I usually _ (go) to school by bus. Right now it _ (rain). She _ (already / finish) her project. We _ (visit) London last summer. They _ (wait) for you since morning. When we came, he _ (sleep).",
                new[] { "go", "is raining", "has already finished", "visited", "have been waiting", "was sleeping" }),
        }),
        new(11, "english-11-word-formation.pvz", "Образование слов (суффиксы)", new[]
        {
            new FillBlankTaskSeed(
                "It is a _ (wonder) day! She sings _ (beautiful). This book is very _ (interest). He runs _ (quick). The weather is _ (sun). Tom is a _ (care) boy.",
                new[] { "wonderful", "beautifully", "interesting", "quickly", "sunny", "careful" }),
        }),
        new(3, "english-03-articles.pvz", "Артикли a / an", new[]
        {
            new FillBlankTaskSeed(
                "I have _ apple. She has _ cat. It is _ orange. We see _ elephant. He is _ doctor. They have _ ball.",
                new[] { "an", "a", "an", "an", "a", "a" }),
            new FillBlankTaskSeed(
                "This is _ book. That is _ old house. I see _ big dog. She eats _ egg. He is _ pupil. It is _ small bird.",
                new[] { "a", "an", "a", "an", "a", "a" }),
        }),
        new(3, "english-03-this-these.pvz", "Указательные местоимения this / these", new[]
        {
            new FillBlankTaskSeed(
                "_ is my pen. (близко, один) _ are my pens. (близко, много) _ is his bag. (далеко, один) _ are her bags. (далеко, много) _ apple is red. (близко, один) _ books are new. (близко, много)",
                new[] { "this", "these", "that", "those", "this", "these" }),
            new FillBlankTaskSeed(
                "_ is a cat. (далеко, один) _ are cats. (далеко, много) _ is my friend. (близко, один) _ are my friends. (близко, много) _ house is big. (далеко, один) _ houses are old. (далеко, много)",
                new[] { "that", "those", "this", "these", "that", "those" }),
        }),
        new(4, "english-04-possessive.pvz", "Притяжательные местоимения my / your / his / her", new[]
        {
            new FillBlankTaskSeed(
                "This is _ (I) pen. Is this _ (you) bag? This is _ (he) ball. That is _ (she) doll. These are _ (we) books. Those are _ (they) toys.",
                new[] { "my", "your", "his", "her", "our", "their" }),
            new FillBlankTaskSeed(
                "_ (I) name is Tom. _ (you) cat is black. _ (he) sister is six. _ (she) brother is ten. _ (we) school is big. _ (they) garden is nice.",
                new[] { "my", "your", "his", "her", "our", "their" }),
        }),
        new(4, "english-04-prepositions-place.pvz", "Предлоги места", new[]
        {
            new FillBlankTaskSeed(
                "The book is _ the table. (на) The cat is _ the box. (в) The ball is _ the chair. (под) The picture is _ the wall. (на) The dog is _ the table and the door. (между) The lamp is _ the desk. (над)",
                new[] { "on", "in", "under", "on", "between", "over|above" }),
            new FillBlankTaskSeed(
                "The pen is _ the bag. (в) The map is _ the wall. (на) The shoes are _ the bed. (под) The shop is _ the school and the park. (между) The clock is _ the door. (над) The cat is _ the sofa. (за)",
                new[] { "in", "on", "under", "between", "over|above", "behind" }),
        }),
        new(5, "english-05-irregular-verbs.pvz", "Неправильные глаголы в Past Simple", new[]
        {
            new FillBlankTaskSeed(
                "go → _. see → _. come → _. eat → _. take → _. give → _.",
                new[] { "went", "saw", "came", "ate", "took", "gave" }),
            new FillBlankTaskSeed(
                "do → _. make → _. get → _. know → _. write → _. read → _.",
                new[] { "did", "made", "got", "knew", "wrote", "read" }),
        }),
        new(6, "english-06-object-pronouns.pvz", "Местоимения в объектном падеже", new[]
        {
            new FillBlankTaskSeed(
                "I see _ (he). She likes _ (I). We help _ (they). He knows _ (she). They call _ (we). Give _ (it) to me.",
                new[] { "him", "me", "them", "her", "us", "it" }),
            new FillBlankTaskSeed(
                "Look at _ (I). Tom helps _ (you). She tells _ (he) a story. We meet _ (they). He loves _ (she). They invite _ (we).",
                new[] { "me", "you", "him", "them", "her", "us" }),
        }),
        new(7, "english-07-prepositions-time.pvz", "Предлоги времени at / on / in", new[]
        {
            new FillBlankTaskSeed(
                "I get up _ 7 o'clock. (at) We have a party _ Sunday. (on) It is cold _ winter. (in) School starts _ the morning. (in) Her birthday is _ the 5th of May. (on) We go to bed _ night. (at)",
                new[] { "at", "on", "in", "in", "on", "at" }),
            new FillBlankTaskSeed(
                "My lesson is _ Monday. (on) The film starts _ 6 p.m. (at) Birds sing _ spring. (in) We have lunch _ noon. (at) It snows _ December. (in) I was born _ 2010. (in)",
                new[] { "on", "at", "in", "at", "in", "in" }),
        }),
    };

    public static TaskbookDocument CreateDocument(FillBlankCollectionSeed seed, string teacherPassword) =>
        FillBlankCollectionBuilder.CreateDocument(TaskbookCatalogSubjects.English, seed, teacherPassword);
}
