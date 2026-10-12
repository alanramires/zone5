---
name: fechamento-do-dia
description: Fecha o dia de trabalho no Zone 5. Escreve o fechamento da versão (série z), faz um commit por frente de trabalho, cria e publica a tag e atualiza o resumo de retomada. Use quando o autor disser "vamos fazer o fechamento do dia", "fechar o dia", "encerrar o dia" ou pedir fechamento + tag + push.
---

# Fechamento do dia (Zone 5)

Este é o ritual do autor, com ordem fixa. **Não pule passos nem troque a ordem.** Cada inversão já custou alguma coisa.

## 0. Antes de tudo: levantar o que aconteceu

```bash
git describe --tags --abbrev=0 --match "z*"   # última tag da série z
git log --oneline <última-tag>..HEAD
git status --porcelain
git diff --stat
```

Pode haver trabalho do autor na árvore que você não acompanhou. Isso é comum, porque ele mexe no Unity em paralelo. Nesse caso, **leia o diff antes de escrever qualquer coisa sobre ele**. Nunca descreva no fechamento ou no commit algo que você não verificou.

Pergunte ao autor sobre o que você não conseguir atribuir a uma frente.

Abrir o projeto numa versão nova do Unity gera upgrade de `ProjectVersion.txt`, migração de `ProjectSettings/*.asset` e reserialização do `UniversalRenderPipelineGlobalSettings.asset`. Isso é uma frente ("engine"), não churn: registre a versão de origem e a de destino.

## 1. Escolher o número da versão

O formato é `zX.Y.Z`, da série z, inaugurada em 11/10/2026. A série antiga, `v0.0.1` a `v0.0.15`, está encerrada; nunca crie tags `v*`. O autor define o número:

| dígito | significa |
|---|---|
| **X** | grande mudança de arquitetura |
| **Y** | mudança localizada importante: uma parte e seus filhos |
| **Z** | salvamento de fim de trabalho |

Na dúvida entre Y e Z, **pergunte**. Não escolha sozinho.

## 2. Escrever `docs/fechamentos/zX.Y.Z.md`

O fechamento é escrito em PT-BR e explica **o porquê**, não o quê. O `git log` já diz o quê.

- Use um título curto que capture o fio do dia, não uma lista.
- Escreva uma seção por frente de trabalho.
- **O que não terminou** tem seção própria. Um fechamento que só conta vitórias não serve para retomar o trabalho.
- Um achado que contradiz o que se acreditava vale mais que uma feature entregue. Escreva com destaque.
- Se uma hipótese sua estava errada e a medição desmentiu, **registre isso**. Sem registro, a próxima sessão repete o erro.
- Diga o que **não** foi verificado no Editor. Não há build nem testes por CLI; compilação e jogo só se confirmam no Unity.

Adicione a linha da versão na tabela do `CHANGELOG.md`, na raiz.

## 3. Um commit por frente de trabalho

**Faça um commit por frente, não um commit do lote inteiro.** Faça o stage arquivo por arquivo, sempre com o `.meta` junto. Nunca use `git add .` ou `git add -A` neste passo.

O ganho é poder reverter uma frente sem tocar nas outras. O ganho oculto é que isso obriga a olhar cada arquivo uma vez, e é assim que se descobre que um arquivo pertence a uma frente inesperada.

Mensagem de commit: `zX.Y.Z - <frente>`. O corpo explica o porquê, cita arquivo e linha quando ajuda e diz o que **não** mudou de propósito. O fechamento e a linha do CHANGELOG vão no commit da última frente, ou num commit `zX.Y.Z - fechamento`.

Termine as mensagens com a linha de atribuição `Co-Authored-By` que estiver em vigor na sessão.

## 4. `git add .`: só agora, e só para o churn

O que sobrou é reserialização do Unity Editor: `SampleScene.unity`, atlas de fonte, `.asset` tocados por reimport.

Commite com uma mensagem dizendo **que é churn** e que não há mudança de design intencional. Se você não tiver certeza de que é churn, pergunte ao autor em vez de afirmar.

Este é o único passo em que `git add .` é permitido.

## 5. Tag: só depois de tudo commitado

```bash
git tag -a zX.Y.Z -m "<o título do fechamento>"
```

**A tag é a última coisa antes do push.** Criar a tag antes do commit final obriga a mover uma referência já publicada com `--force`, o que sobrescreve o que outros possam ter puxado.

## 6. Push do commit e da tag

Confirme com o autor antes. Os assets PSD e PNG vão pelo Git LFS e podem ser pesados.

```bash
git push origin main
git push origin zX.Y.Z
```

## 7. Atualizar `docs/resumo.md`

O resumo é o handoff para a próxima conversa. **Resumo desatualizado é pior que resumo nenhum**, porque uma conversa nova acredita nele.

Atualize o resumo depois da tag, num commit próprio (`resumo pós-zX.Y.Z`). Ele descreve o estado *pós-versão* e não faz parte dela. Faça o push desse commit também.

Atualize:

- **Estado**: a versão nova, a versão do Unity e a descoberta que organiza o que vem.
- **Onde eu parei**: o que ficou pela metade, com nomes de arquivo.
- **Armadilhas**: toda armadilha que custou tempo hoje entra na tabela.
- **Refactor**: em que fase do `REFACTOR_PLAN.md` estamos.

Corte o que virou obsoleto. O resumo cresce se ninguém podar.

## Critério de pronto

- [ ] `git status` limpo
- [ ] `docs/fechamentos/zX.Y.Z.md` existe e tem a seção do que não terminou
- [ ] `CHANGELOG.md` tem a linha da versão
- [ ] um commit por frente, mais um de churn se houver
- [ ] tag anotada criada **depois** do último commit da versão, e com push feito
- [ ] `docs/resumo.md` descreve o estado de agora, com push feito

Relate ao autor o que ficou pendente e o que você não conseguiu verificar.
