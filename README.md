# MathClaims

Sistema de proteção por células de 16×16 em X/Z, abrangendo a altura inteira do mundo, para Vintage Story 1.22.7. O estado é de autoridade do servidor; o cliente nunca é a fonte de verdade. Embora o chunk técnico do jogo seja 32×32, cada um contém quatro células MathClaims.

## Estado desta entrega

O núcleo e a integração real com o mapa vanilla carregam no servidor e no cliente oficiais 1.22.7. Ainda há itens de acabamento e a conversão explícita de claims vanilla antes de considerar uma publicação pública.

### Implementado

- Modelo de célula 16×16 com dimensão, coordenadas negativas corretas e índice `célula -> propriedade`.
- Migração automática e crash-safe do schema próprio v1 (32×32) para v2
  (16×16): cada célula antiga expande para quatro novas sem reduzir a área;
  o JSON anterior permanece em `claims-v1.json.bak`.
- Propriedades conectadas apenas nos quatro lados; diagonais não conectam.
- Nome obrigatório e validado (trim, 1–48 caracteres seguros).
- Limites padrão: 3 propriedades e 64 células de 16×16 por jogador.
- Recusa de ponte que conectaria duas propriedades do mesmo dono.
- Remoção com cálculo prévio de componentes e recusa quando o split excede o limite.
- Transferência atômica em memória e merge por adjacência; os metadados da propriedade preexistente do destinatário prevalecem.
- Permissões por jogador e cálculo verde/amarelo/vermelho.
- Persistência JSON versionada, com escrita temporária e replace/backup.
- Auditoria persistente em `UserData/MathClaims/logs/audit.log`.
- Contador de inatividade baseado em uptime, com verificação a cada 60 segundos e privilege `mathclaims.noexpire`.
- Interceptação server-side para place/break, uso de blocos e interações com entidades, com a autorização consultando o índice de colunas.
- Interações de portas, contêineres e máquinas exigem a categoria exata; tipos
  interativos desconhecidos adotam a regra conservadora de máquina. Água, lava e
  outros blocos líquidos exigem a permissão **Líquidos**; interações com entidades
  exigem **Animais**.
- O servidor consome a família de comandos vanilla `/land` quando MathClaims
  está habilitado e direciona o jogador ao mapa, sem afetar comandos como
  `/landscape` ou o chat comum.
- Seleção no mapa vanilla: clique direito, arraste retangular conectado e seleção manual com Shift. Arrastar mantendo Shift cria um rastro cardinal contínuo, em vez de um retângulo.
- A pré-seleção usa moldura roxa; somente claims consolidadas usam verde (dono), amarelo (com acesso) ou vermelho (sem acesso).
- Menu contextual sobre o mapa para criar waypoint ou **Proteger**. Antes de mostrar o nome, o cliente pede uma pré-validação ao servidor: célula já protegida, spawn ou outra regra inválida recebe feedback sem abrir formulário.
- Se qualquer célula de uma seleção tocar lateralmente uma única propriedade do próprio jogador, toda a seleção é adicionada a ela e não pede novo nome. Uma seleção que uniria duas propriedades ainda é recusada pelo servidor.
- Revisão visual do mapa: um único controlador de diálogos impede compositores duplicados; o menu contém apenas **Criar waypoint** e **Proteger** e fecha pelo X. O arraste recalcula a área quando encolhido e não processa entrada enquanto um formulário está aberto.
- Snapshot de claims por jogador via canal de rede, atualizado depois de criação de claim.
- Clique direito sobre uma claim já conhecida abre informações; proprietário ou
  gestor autorizado pode abrir a primeira tela de gerenciamento e renomear a
  propriedade. A autorização é recalculada pelo servidor ao salvar.
- A tela de gerenciamento possui **Gerenciar membros**: uma lista pesquisável,
  paginada e com marcador visual de cada jogador conhecido. Escolher um membro
  abre somente as permissões dele; ao salvar, retorna à lista. Permissões vazias
  removem o acesso. O servidor resolve e autoriza cada alteração pelo UID e
  registra a operação na auditoria.
- Cada proprietário pode ativar ou desativar **PvP** por propriedade. Desligado
  por padrão, ele impede dano originado de outro jogador tanto contra quem está
  na própria propriedade quanto partindo de quem está nela; dano de ambiente e
  de criaturas não é afetado. A decisão é aplicada no servidor.
- Um clique direito sobre o ícone de um waypoint preserva a prioridade vanilla e
  abre a edição daquele waypoint, em vez de abrir uma tela do MathClaims.
- Transferência integral e abandono estão disponíveis no mesmo gerenciamento,
  ambos com confirmação. A transferência valida o destinatário e todos os
  limites antes da mutação; abandono remove somente a proteção.
- A partir da tela de gerenciamento, **Remover terreno selecionado** libera a
  célula que abriu a tela. O servidor confirma que ela pertence à propriedade,
  calcula os componentes antes de alterar o estado e recusa splits que
  excederiam o limite de propriedades.
- `Ctrl+P` alterna a visualização física client-side: linhas verticais nos
  cantos, limite superior e linha inferior seguindo a superfície. A consulta é
  limitada ao entorno do jogador pelo índice espacial. A região de spawn
  protegida aparece em branco quando a visualização está ligada.
- Região sem claim baseada no spawn real do mundo e distância mínima configurável entre donos diferentes. Por padrão, spawn é apenas uma região sem novas claims: construção e interações continuam liberadas até que o administrador ative a proteção.
- `/mathclaim` abre a GUI administrativa para quem tem `controlserver`. Ela envia
  uma proposta única ao servidor para configurações gerais, limites, distância,
  spawn (incluindo as sete restrições) e expiração; o servidor valida, limita,
  persiste, audita e republica o snapshot visual.
- A preferência administrativa **Mostrar em área não descoberta** é aplicada no
  cliente: desligada, a camada só desenha claims em setores que o cliente já
  carregou ao redor do jogador; ligada, desenha todas as claims recebidas. O
  cache guarda somente coordenadas de setores 32×32, nunca conteúdo de mapa,
  bioma ou relevo, portanto não revela terreno.
- O mesmo painel consulta a lista real de `LandClaims` vanilla e mostra a
  contagem legada sem tocar nesses dados. Se houver claims, `/mathclaim` também
  emite um aviso administrativo explícito.
- Administradores veem **Ações administrativas** ao consultar uma propriedade:
  transferência forçada e exclusão têm confirmação, são revalidadas pelo
  servidor e a exclusão libera somente a proteção/metadados.
- A GUI administrativa também mostra quantas proteções vanilla de casas/story
  reconhecidas existem e oferece **Liberar casas NPC/story**, sempre com
  confirmação. A ação remove somente `LandClaims` cuja chave interna é uma das
  três conhecidas (`custommessage-nadiya`, `custommessage-tobias` e
  `custommessage-treasurehunter`), registra auditoria e não remove blocos.
  Claims vanilla de jogadores, inclusive nomes parecidos, nunca entram nessa
  ação.

## Compilar

O projeto referencia por padrão a instalação de teste oficial em `/run/media/matheider/armazem/Servers/Vintage_Story/1.22.7`.

```sh
dotnet run -c Release --project tests/MathClaims.Core.Tests.csproj
dotnet build -c Release MathClaims.csproj
```

Para usar outra instalação, informe `-p:VintageStoryPath=/caminho/para/VintageStory`.

A build instalável para cliente fica em `bin/Release/Mods/mathclaims_1.0.1.zip`;
o diretório solto para o servidor fica em `bin/Release/Mods/mathclaims/`.
O alvo de empacotamento é executado pelo próprio `dotnet build`.

## Instalação de teste

Copie o diretório `mathclaims` para `UserData/Mods/` da instância do servidor. Nesta máquina, a cópia de teste está em:

`/run/media/matheider/armazem/Servers/Vintage_Story/1.22.7/UserData/Mods/mathclaims`

O servidor de teste usa dados isolados em `UserData/`; nenhum mundo existente foi apagado ou resetado.

As evidências da validação visual em cliente real ficam em [`qa/README.md`](qa/README.md).

## Configuração e dados

- Configuração: `UserData/ModConfig/mathclaims.json`
- Estado: `UserData/MathClaims/claims-v1.json`
- Auditoria: `UserData/MathClaims/logs/audit.log`
- Cache local de visibilidade de exploração:
  `VintagestoryData/ModConfig/mathclaims-exploration.json` (cliente)

Os valores padrão são `MaxPropertiesPerPlayer=3`, `MaxChunksPerPlayer=64`, expiração desligada e referência de 30 dias de uptime quando ativada.

## Controles e administração

No mapa (`M`), botão direito seleciona e abre o menu **Criar waypoint / Proteger**. Arraste com o botão direito para uma área conectada. Com `Shift`, clique em células 16×16 conectadas ou arraste para deixar um rastro conectado; solte `Shift` para abrir o menu. A seleção roxa ainda não é uma claim. `Shift` + botão direito sobre uma propriedade que você gerencia seleciona, em vermelho, células a desproteger e pede confirmação explícita. Clique direito no ícone de waypoint edita o waypoint vanilla; clique direito numa claim existente abre suas informações e, quando permitido, **Gerenciar propriedade**. `Ctrl+P` alterna as bordas 3D de claims e da proteção de spawn. `/mathclaim`, para quem tem `controlserver`, abre a GUI de configurações administrativas.

## Roadmap (não implementado nesta entrega)

- Deltas de rede espaciais (o snapshot inicial atual ainda é global).
- Refinamento do renderer `Ctrl+P` (malha em lote e tratamento de relevo em áreas parcialmente carregadas).
- Leitura direta da máscara interna de exploração do mapa vanilla, se a API
  pública passar a expô-la. A versão atual já cumpre a privacidade por um cache
  conservador de setores carregados, sem inspecionar APIs privadas.
- Conversão explícita de `LandClaims` vanilla (a detecção/aviso e bloqueio de `/land` já existem; o mod não apaga claims legadas automaticamente).
- Economia, venda, reembolso, marketplace e bônus de expiração continuam fora da V1 planejada.

## Licença e proveniência

O código deste repositório foi escrito para MathClaims. Nenhum código Swixy ClaimChunk foi copiado. Antes de publicação pública, adicione uma licença compatível e faça uma revisão de segurança/compatibilidade completa.
